using System.Text.Json;
using System.Text.Json.Serialization;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Infrastructure.Storage;

public sealed class JsonRecheckTaskStore : IRecheckTaskStore
{
    private readonly string _taskPath;
    private readonly string _executionPath;
    private readonly string _latestResultPath;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public JsonRecheckTaskStore(
        string? taskPath = null,
        string? executionPath = null,
        string? latestResultPath = null)
    {
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TylinkInspection");

        _taskPath = taskPath ?? Path.Combine(baseDirectory, "recheck-tasks.json");
        _executionPath = executionPath ?? Path.Combine(baseDirectory, "recheck-executions.json");
        _latestResultPath = latestResultPath ?? Path.Combine(baseDirectory, "recheck-latest-results.json");
    }

    public IReadOnlyList<RecheckTaskRecord> LoadTasks()
    {
        return LoadFile<List<RecheckTaskRecord>>(_taskPath) ?? [];
    }

    public void SaveTasks(IReadOnlyList<RecheckTaskRecord> tasks)
    {
        SaveFile(_taskPath, tasks);
    }

    public IReadOnlyList<RecheckExecutionRecord> LoadExecutions()
    {
        return LoadFile<List<RecheckExecutionRecord>>(_executionPath) ?? [];
    }

    public void SaveExecutions(IReadOnlyList<RecheckExecutionRecord> executions)
    {
        SaveFile(_executionPath, executions);
    }

    public IReadOnlyList<RecheckTaskLatestResult> LoadLatestResults()
    {
        return LoadFile<List<RecheckTaskLatestResult>>(_latestResultPath) ?? [];
    }

    public void SaveLatestResults(IReadOnlyList<RecheckTaskLatestResult> latestResults)
    {
        SaveFile(_latestResultPath, latestResults);
    }

    private T? LoadFile<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, _serializerOptions);
    }

    private void SaveFile<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(value, _serializerOptions);
        File.WriteAllText(path, json);
    }
}
