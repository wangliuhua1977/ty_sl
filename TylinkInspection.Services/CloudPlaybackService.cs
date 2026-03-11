using System.Globalization;
using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Services;

public sealed class CloudPlaybackService : OpenPlatformAlarmServiceBase, ICloudPlaybackService
{
    private const string FolderListEndpoint = "/open/token/cloud/getCloudFolderList";
    private const string FileListEndpoint = "/open/token/cloud/getCloudFileList";
    private const string DownloadUrlEndpoint = "/open/token/cloud/getFileUrlById";
    private const string StreamUrlHlsEndpoint = "/open/token/cloud/streamUrlHls";
    private const string StreamUrlRtmpEndpoint = "/open/token/cloud/streamUrlRtmp";
    private static readonly StringComparer TextComparer = StringComparer.OrdinalIgnoreCase;

    private readonly ICloudPlaybackCacheStore _store;
    private readonly object _syncRoot = new();

    public CloudPlaybackService(
        IOpenPlatformOptionsProvider optionsProvider,
        ITokenService tokenService,
        IOpenPlatformClient openPlatformClient,
        ICloudPlaybackCacheStore store)
        : base(optionsProvider, tokenService, openPlatformClient)
    {
        _store = store;
    }

    public CloudPlaybackQueryResult GetRecentFiles(string deviceCode, string deviceName, int? netTypeCode, bool forceRefresh = false, int take = 6)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            throw new InvalidOperationException("设备编码不能为空，无法查询云回看文件。");
        }

        var normalizedCode = deviceCode.Trim();
        var normalizedName = string.IsNullOrWhiteSpace(deviceName) ? normalizedCode : deviceName.Trim();
        var now = DateTimeOffset.Now;
        var cachedEntry = GetCachedRecentFiles(normalizedCode);

        if (!forceRefresh && cachedEntry is not null && cachedEntry.CachedAt >= now.AddMinutes(-15))
        {
            return new CloudPlaybackQueryResult
            {
                DeviceCode = cachedEntry.DeviceCode,
                DeviceName = cachedEntry.DeviceName,
                QueriedAt = cachedEntry.CachedAt,
                FromCache = true,
                Files = cachedEntry.Files
            };
        }

        try
        {
            var folders = LoadFolders(normalizedCode, netTypeCode);
            if (folders.Count == 0)
            {
                folders = BuildFallbackFolders(now);
            }

            var files = LoadRecentFiles(normalizedCode, netTypeCode, folders, take);
            var cacheEntry = new CloudPlaybackCacheEntry
            {
                DeviceCode = normalizedCode,
                DeviceName = normalizedName,
                CachedAt = now,
                Files = files
            };

            SaveCache(cacheEntry);

            return new CloudPlaybackQueryResult
            {
                DeviceCode = normalizedCode,
                DeviceName = normalizedName,
                QueriedAt = now,
                FromCache = false,
                Files = files
            };
        }
        catch when (cachedEntry is not null)
        {
            return new CloudPlaybackQueryResult
            {
                DeviceCode = cachedEntry.DeviceCode,
                DeviceName = cachedEntry.DeviceName,
                QueriedAt = cachedEntry.CachedAt,
                FromCache = true,
                Files = cachedEntry.Files
            };
        }
    }

    public CloudPlaybackCacheEntry? GetCachedRecentFiles(string deviceCode)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            return null;
        }

        lock (_syncRoot)
        {
            return _store.Load()
                .FirstOrDefault(item => TextComparer.Equals(item.DeviceCode, deviceCode.Trim()));
        }
    }

    public CloudPlaybackResolutionResult ResolveFileStreams(string deviceCode, string deviceName, int? netTypeCode, CloudPlaybackFile file, bool forceRefresh = false)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            throw new InvalidOperationException("设备编码不能为空，无法获取回看流化地址。");
        }

        if (string.IsNullOrWhiteSpace(file.Id))
        {
            throw new InvalidOperationException("回看文件标识不能为空，无法获取回看流化地址。");
        }

        var normalizedCode = deviceCode.Trim();
        var normalizedName = string.IsNullOrWhiteSpace(deviceName) ? normalizedCode : deviceName.Trim();
        var now = DateTimeOffset.Now;
        var cachedFile = GetCachedRecentFiles(normalizedCode)?.Files
            .FirstOrDefault(item => TextComparer.Equals(item.Id, file.Id));

        if (!forceRefresh && cachedFile is not null && IsResolvedStreamFresh(cachedFile, now))
        {
            return new CloudPlaybackResolutionResult
            {
                DeviceCode = normalizedCode,
                DeviceName = normalizedName,
                ResolvedAt = cachedFile.StreamResolvedAt ?? now,
                FromCache = true,
                File = cachedFile,
                DiagnosticMessage = string.Empty
            };
        }

        var diagnostics = new List<string>();
        var hlsUrl = TryResolveUrl(
            () => ExecuteText(StreamUrlHlsEndpoint, BuildStreamHlsParameters(normalizedCode, file.Id)),
            diagnostics,
            "HLS 回看流化");
        var rtmpUrl = TryResolveUrl(
            () => ExecuteText(StreamUrlRtmpEndpoint, BuildStreamRtmpParameters(normalizedCode, file.Id, netTypeCode)),
            diagnostics,
            "RTMP 回看流化");
        var downloadUrl = TryResolveUrl(
            () => ExecuteText(DownloadUrlEndpoint, BuildDownloadParameters(normalizedCode, file.Id, netTypeCode)),
            diagnostics,
            "回看下载地址");

        var resolvedFile = new CloudPlaybackFile
        {
            Id = file.Id,
            Name = file.Name,
            IconUrl = file.IconUrl,
            FileType = file.FileType,
            Size = file.Size,
            CreateTime = file.CreateTime,
            StreamResolvedAt = now,
            HlsStreamUrl = hlsUrl,
            RtmpStreamUrl = rtmpUrl,
            DownloadUrl = downloadUrl
        };

        MergeResolvedFile(normalizedCode, normalizedName, resolvedFile);

        return new CloudPlaybackResolutionResult
        {
            DeviceCode = normalizedCode,
            DeviceName = normalizedName,
            ResolvedAt = now,
            FromCache = false,
            File = resolvedFile,
            DiagnosticMessage = JoinDiagnostics(diagnostics, resolvedFile)
        };
    }

    private IReadOnlyList<string> LoadFolders(string deviceCode, int? netTypeCode)
    {
        var payload = UnwrapResponseData(Execute(FolderListEndpoint, BuildFolderParameters(deviceCode, netTypeCode)).Data);
        return ReadArray(payload, "fileList", "list")
            .Select(item => ReadString(item, "name"))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .OrderByDescending(ParseFolderSortValue)
            .Take(3)
            .ToList();
    }

    private IReadOnlyList<CloudPlaybackFile> LoadRecentFiles(
        string deviceCode,
        int? netTypeCode,
        IReadOnlyList<string> folders,
        int take)
    {
        var items = new List<CloudPlaybackFile>();
        var pageSize = Math.Clamp(Math.Max(take, 10), 10, 50);

        foreach (var folder in folders)
        {
            var payload = UnwrapResponseData(Execute(FileListEndpoint, BuildFileParameters(deviceCode, netTypeCode, folder, pageSize)).Data);
            foreach (var item in ReadArray(payload, "list", "fileList"))
            {
                var mapped = new CloudPlaybackFile
                {
                    Id = ReadString(item, "id"),
                    Name = ReadString(item, "name"),
                    IconUrl = ReadString(item, "iconUrl"),
                    FileType = NormalizeText(ReadString(item, "fileType"), "unknown"),
                    Size = ReadInt64(item, "size") ?? 0,
                    CreateTime = ReadDateTimeOffset(item, "createDate"),
                    StreamResolvedAt = null,
                    HlsStreamUrl = null,
                    RtmpStreamUrl = null,
                    DownloadUrl = null
                };

                if (string.IsNullOrWhiteSpace(mapped.Id))
                {
                    continue;
                }

                if (items.Any(existing => TextComparer.Equals(existing.Id, mapped.Id)))
                {
                    continue;
                }

                items.Add(mapped);
            }

            if (items.Count >= take)
            {
                break;
            }
        }

        return items
            .OrderByDescending(item => item.CreateTime ?? DateTimeOffset.MinValue)
            .Take(take)
            .ToList();
    }

    private void MergeResolvedFile(string deviceCode, string deviceName, CloudPlaybackFile resolvedFile)
    {
        lock (_syncRoot)
        {
            var items = _store.Load().ToList();
            var existingEntryIndex = items.FindIndex(item => TextComparer.Equals(item.DeviceCode, deviceCode));
            if (existingEntryIndex < 0)
            {
                items.Add(new CloudPlaybackCacheEntry
                {
                    DeviceCode = deviceCode,
                    DeviceName = deviceName,
                    CachedAt = DateTimeOffset.Now,
                    Files = [resolvedFile]
                });
            }
            else
            {
                var existingEntry = items[existingEntryIndex];
                var files = existingEntry.Files
                    .Where(item => !TextComparer.Equals(item.Id, resolvedFile.Id))
                    .ToList();
                files.Add(resolvedFile);

                items[existingEntryIndex] = new CloudPlaybackCacheEntry
                {
                    DeviceCode = existingEntry.DeviceCode,
                    DeviceName = existingEntry.DeviceName,
                    CachedAt = existingEntry.CachedAt,
                    Files = files
                        .OrderByDescending(item => item.CreateTime ?? DateTimeOffset.MinValue)
                        .ToList()
                };
            }

            _store.Save(items
                .OrderBy(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }
    }

    private void SaveCache(CloudPlaybackCacheEntry entry)
    {
        lock (_syncRoot)
        {
            var items = _store.Load()
                .Where(item => !TextComparer.Equals(item.DeviceCode, entry.DeviceCode))
                .ToList();
            items.Add(entry);

            _store.Save(items
                .OrderBy(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }
    }

    private static string TryResolveUrl(
        Func<OpenPlatformResponseEnvelope<string>> execute,
        ICollection<string> diagnostics,
        string label)
    {
        try
        {
            var response = execute();
            return ResolveTextUrl(response.Data);
        }
        catch (PlatformServiceException ex)
        {
            diagnostics.Add($"{label}失败：{SummarizeFailure(ex.Message)}");
            return string.Empty;
        }
        catch (Exception ex)
        {
            diagnostics.Add($"{label}失败：{SummarizeFailure(ex.Message)}");
            return string.Empty;
        }
    }

    private static string ResolveTextUrl(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

        var trimmed = payload.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            using var document = JsonDocument.Parse(trimmed);
            var root = document.RootElement;
            return ReadString(root, "url", "streamUrl");
        }

        if (trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            return JsonSerializer.Deserialize<string>(trimmed) ?? string.Empty;
        }

        return trimmed;
    }

    private static bool IsResolvedStreamFresh(CloudPlaybackFile file, DateTimeOffset now)
    {
        return file.StreamResolvedAt.HasValue &&
               file.StreamResolvedAt.Value >= now.AddMinutes(-5) &&
               (!string.IsNullOrWhiteSpace(file.HlsStreamUrl) ||
                !string.IsNullOrWhiteSpace(file.RtmpStreamUrl) ||
                !string.IsNullOrWhiteSpace(file.DownloadUrl));
    }

    private static string JoinDiagnostics(IEnumerable<string> diagnostics, CloudPlaybackFile file)
    {
        var items = diagnostics
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (items.Count == 0)
        {
            if (!file.HasStreamingAddress)
            {
                items.Add("当前未获取到可播放的回看流化地址。");
            }
            else if (string.IsNullOrWhiteSpace(file.HlsStreamUrl) && !string.IsNullOrWhiteSpace(file.RtmpStreamUrl))
            {
                items.Add("当前仅获取到 RTMP 回看地址，浏览器宿主会尝试但不保证可播。");
            }
        }

        return string.Join(" ", items);
    }

    private static Dictionary<string, string> BuildFolderParameters(string deviceCode, int? netTypeCode)
    {
        var parameters = new Dictionary<string, string>
        {
            ["deviceCode"] = deviceCode
        };

        if (netTypeCode is 0 or 1)
        {
            parameters["netType"] = netTypeCode.Value.ToString(CultureInfo.InvariantCulture);
        }

        return parameters;
    }

    private static Dictionary<string, string> BuildFileParameters(string deviceCode, int? netTypeCode, string path, int pageSize)
    {
        var parameters = new Dictionary<string, string>
        {
            ["deviceCode"] = deviceCode,
            ["path"] = path,
            ["type"] = "1",
            ["orderBy"] = "1",
            ["pageNo"] = "1",
            ["pageSize"] = pageSize.ToString(CultureInfo.InvariantCulture)
        };

        if (netTypeCode is 0 or 1)
        {
            parameters["netType"] = netTypeCode.Value.ToString(CultureInfo.InvariantCulture);
        }

        return parameters;
    }

    private static Dictionary<string, string> BuildStreamHlsParameters(string deviceCode, string fileId)
    {
        return new Dictionary<string, string>
        {
            ["deviceCode"] = deviceCode,
            ["fileId"] = fileId,
            ["mute"] = "1",
            ["type"] = "4"
        };
    }

    private static Dictionary<string, string> BuildStreamRtmpParameters(string deviceCode, string fileId, int? netTypeCode)
    {
        var parameters = new Dictionary<string, string>
        {
            ["deviceCode"] = deviceCode,
            ["fileId"] = fileId
        };

        if (netTypeCode is 0 or 1)
        {
            parameters["netType"] = netTypeCode.Value.ToString(CultureInfo.InvariantCulture);
        }

        return parameters;
    }

    private static Dictionary<string, string> BuildDownloadParameters(string deviceCode, string fileId, int? netTypeCode)
    {
        var parameters = new Dictionary<string, string>
        {
            ["deviceCode"] = deviceCode,
            ["id"] = fileId,
            ["isHttp"] = "0"
        };

        if (netTypeCode is 0 or 1)
        {
            parameters["netType"] = netTypeCode.Value.ToString(CultureInfo.InvariantCulture);
        }

        return parameters;
    }

    private static IReadOnlyList<string> BuildFallbackFolders(DateTimeOffset now)
    {
        return
        [
            now.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            now.AddDays(-1).ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            now.AddDays(-2).ToString("yyyyMMdd", CultureInfo.InvariantCulture)
        ];
    }

    private static long ParseFolderSortValue(string folderName)
    {
        if (DateTime.TryParseExact(folderName, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed.Ticks;
        }

        return 0;
    }

    private static string SummarizeFailure(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "开放平台调用失败。";
        }

        var normalized = message.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
        return normalized.Length <= 120
            ? normalized
            : $"{normalized[..119]}…";
    }
}
