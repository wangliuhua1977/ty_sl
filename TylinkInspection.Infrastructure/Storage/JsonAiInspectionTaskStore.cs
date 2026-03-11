using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Infrastructure.Storage;

public sealed class JsonAiInspectionTaskStore : IAiInspectionTaskStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _storagePath;

    public JsonAiInspectionTaskStore(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TylinkInspection",
            "ai-inspection-tasks.json");
    }

    public IReadOnlyList<AiInspectionTaskBatch> LoadAll()
    {
        if (!File.Exists(_storagePath))
        {
            return Array.Empty<AiInspectionTaskBatch>();
        }

        var json = File.ReadAllText(_storagePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<AiInspectionTaskBatch>();
        }

        try
        {
            var batches = JsonSerializer.Deserialize<List<AiInspectionTaskBatch>>(json, SerializerOptions);
            if (batches is not null)
            {
                return batches;
            }
        }
        catch
        {
        }

        return TryLoadLegacyTasks(json);
    }

    public void SaveAll(IReadOnlyList<AiInspectionTaskBatch> tasks)
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(tasks, SerializerOptions);
        File.WriteAllText(_storagePath, json);
    }

    private static IReadOnlyList<AiInspectionTaskBatch> TryLoadLegacyTasks(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<AiInspectionTaskBatch>();
            }

            var migrated = new List<AiInspectionTaskBatch>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var taskId = ReadString(element, "taskId");
                var title = ReadString(element, "title");
                var deviceCode = ReadString(element, "deviceCode");
                if (string.IsNullOrWhiteSpace(taskId) || string.IsNullOrWhiteSpace(deviceCode))
                {
                    continue;
                }

                var startedAt = ReadDateTimeOffset(element, "startedAt");
                var finishedAt = ReadDateTimeOffset(element, "finishedAt");
                var status = MigrateLegacyStatus(ReadString(element, "status"), finishedAt);
                var itemStatus = status switch
                {
                    AiInspectionTaskStatus.Succeeded => AiInspectionTaskItemStatus.Succeeded,
                    AiInspectionTaskStatus.Failed => AiInspectionTaskItemStatus.Failed,
                    AiInspectionTaskStatus.Canceled => AiInspectionTaskItemStatus.Canceled,
                    AiInspectionTaskStatus.Running => AiInspectionTaskItemStatus.Running,
                    _ => AiInspectionTaskItemStatus.Pending
                };

                migrated.Add(new AiInspectionTaskBatch
                {
                    TaskId = taskId,
                    TaskName = string.IsNullOrWhiteSpace(title) ? $"Legacy Task {taskId}" : title,
                    SchemeId = "legacy",
                    SchemeName = ReadString(element, "regionName"),
                    TaskType = AiInspectionTaskType.BasicInspection,
                    ScopeMode = AiInspectionTaskScopeMode.FullScheme,
                    TotalCount = 1,
                    SucceededCount = string.Equals(itemStatus, AiInspectionTaskItemStatus.Succeeded, StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                    FailedCount = string.Equals(itemStatus, AiInspectionTaskItemStatus.Failed, StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                    AbnormalCount = string.Equals(itemStatus, AiInspectionTaskItemStatus.Failed, StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                    CanceledCount = string.Equals(itemStatus, AiInspectionTaskItemStatus.Canceled, StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                    Status = status,
                    CreatedAt = ReadDateTimeOffset(element, "scheduledAt") ?? DateTimeOffset.Now,
                    StartedAt = startedAt,
                    CompletedAt = finishedAt,
                    CreatedBy = "legacy",
                    FailureSummary = ReadString(element, "latestNote"),
                    LatestResultSummary = ReadString(element, "description"),
                    Items =
                    [
                        new AiInspectionTaskItem
                        {
                            ItemId = $"{taskId}-legacy",
                            DeviceCode = deviceCode,
                            DeviceName = title,
                            DirectoryPath = ReadString(element, "regionName"),
                            ExecutionStatus = itemStatus,
                            LastError = string.Equals(itemStatus, AiInspectionTaskItemStatus.Failed, StringComparison.OrdinalIgnoreCase)
                                ? ReadString(element, "latestNote")
                                : string.Empty,
                            LastResultSummary = ReadString(element, "description"),
                            IsAbnormalResult = string.Equals(itemStatus, AiInspectionTaskItemStatus.Failed, StringComparison.OrdinalIgnoreCase),
                            StartedAt = startedAt,
                            CompletedAt = finishedAt
                        }
                    ],
                    ExecutionRecords = ReadLegacyExecutionRecords(element, taskId, deviceCode, title)
                });
            }

            return migrated;
        }
        catch
        {
            return Array.Empty<AiInspectionTaskBatch>();
        }
    }

    private static IReadOnlyList<AiInspectionTaskExecutionRecord> ReadLegacyExecutionRecords(JsonElement element, string taskId, string deviceCode, string deviceName)
    {
        if (!element.TryGetProperty("executionRecords", out var recordsElement) ||
            recordsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<AiInspectionTaskExecutionRecord>();
        }

        var records = new List<AiInspectionTaskExecutionRecord>();
        foreach (var recordElement in recordsElement.EnumerateArray())
        {
            records.Add(new AiInspectionTaskExecutionRecord
            {
                RecordId = ReadString(recordElement, "recordId", Guid.NewGuid().ToString("N")),
                TaskId = taskId,
                ItemId = $"{taskId}-legacy",
                DeviceCode = deviceCode,
                DeviceName = deviceName,
                Timestamp = ReadDateTimeOffset(recordElement, "timestamp") ?? DateTimeOffset.Now,
                Message = ReadString(recordElement, "message"),
                AccentResourceKey = ReadString(recordElement, "accentResourceKey", "TonePrimaryBrush")
            });
        }

        return records;
    }

    private static string MigrateLegacyStatus(string? status, DateTimeOffset? finishedAt)
    {
        if (string.Equals(status, AiInspectionTaskStatus.Pending, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, AiInspectionTaskStatus.Running, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, AiInspectionTaskStatus.Succeeded, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, AiInspectionTaskStatus.Failed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, AiInspectionTaskStatus.PartiallyCompleted, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, AiInspectionTaskStatus.Canceled, StringComparison.OrdinalIgnoreCase))
        {
            return status!;
        }

        return finishedAt.HasValue ? AiInspectionTaskStatus.Succeeded : AiInspectionTaskStatus.Pending;
    }

    private static string ReadString(JsonElement element, string name, string fallback = "")
    {
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : fallback;
    }

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(property.GetString(), out var value)
            ? value
            : null;
    }
}
