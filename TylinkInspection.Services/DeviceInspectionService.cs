using System.Globalization;
using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Services;

public sealed class DeviceInspectionService : OpenPlatformAlarmServiceBase, IDeviceInspectionService
{
    private const string DeviceStatusEndpoint = "/open/token/vpaas/device/getDeviceStatus";
    private const string H5StreamEndpoint = "/open/token/vpaas/getH5StreamUrl";
    private const string WebRtcStreamEndpoint = "/open/token/vpaas/getDeviceMediaWebrtcUrl";
    private const string HlsStreamEndpoint = "/open/token/cloud/getDeviceMediaUrlHls";
    private const string FlvStreamEndpoint = "/open/token/cloud/getDeviceMediaUrlFlv";
    private static readonly StringComparer TextComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IDeviceInspectionStore _store;
    private readonly object _syncRoot = new();

    public DeviceInspectionService(
        IOpenPlatformOptionsProvider optionsProvider,
        ITokenService tokenService,
        IOpenPlatformClient openPlatformClient,
        IDeviceInspectionStore store)
        : base(optionsProvider, tokenService, openPlatformClient)
    {
        _store = store;
    }

    public DeviceInspectionResult Inspect(DevicePointProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return InspectCore(profile.Device.DeviceCode, profile.Device.DeviceName, profile.Device.NetTypeCode);
    }

    public DeviceInspectionResult Inspect(InspectionScopeDevice scopeDevice)
    {
        ArgumentNullException.ThrowIfNull(scopeDevice);
        return InspectCore(scopeDevice.Device.DeviceCode, scopeDevice.Device.DeviceName, scopeDevice.Device.NetTypeCode);
    }

    public DeviceInspectionResult? GetLatestResult(string deviceCode)
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

    public IReadOnlyDictionary<string, DeviceInspectionResult> GetLatestResults(IEnumerable<string> deviceCodes)
    {
        ArgumentNullException.ThrowIfNull(deviceCodes);

        var targetCodes = new HashSet<string>(
            deviceCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim()),
            TextComparer);

        if (targetCodes.Count == 0)
        {
            return new Dictionary<string, DeviceInspectionResult>(TextComparer);
        }

        lock (_syncRoot)
        {
            return _store.Load()
                .Where(item => targetCodes.Contains(item.DeviceCode))
                .GroupBy(item => item.DeviceCode, TextComparer)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.InspectionTime).First(), TextComparer);
        }
    }

    private DeviceInspectionResult InspectCore(string? deviceCode, string? deviceName, int? netTypeCode)
    {
        var normalizedCode = deviceCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new InvalidOperationException("设备编码不能为空，无法执行基础巡检。");
        }

        var normalizedName = string.IsNullOrWhiteSpace(deviceName)
            ? normalizedCode
            : deviceName.Trim();
        var inspectionTime = DateTimeOffset.Now;

        try
        {
            var onlineStatus = QueryOnlineStatus(normalizedCode);
            if (onlineStatus != 1)
            {
                return SaveResult(new DeviceInspectionResult
                {
                    DeviceCode = normalizedCode,
                    DeviceName = normalizedName,
                    OnlineStatus = onlineStatus,
                    InspectionTime = inspectionTime,
                    PreferredProtocol = string.Empty,
                    FallbackProtocol = string.Empty,
                    PreferredUrl = string.Empty,
                    FallbackUrl = string.Empty,
                    ExpireTime = null,
                    VideoEnc = "未知",
                    PlaybackHealthGrade = PlaybackHealthGrade.E,
                    FailureReason = BuildOfflineFailureReason(onlineStatus),
                    Suggestion = BuildOfflineSuggestion(onlineStatus),
                    NeedRecheck = true
                });
            }

            var diagnostics = new List<string>();
            var streamBundle = TryLoadH5Streams(normalizedCode, netTypeCode, inspectionTime, diagnostics);
            var candidates = streamBundle.Candidates.ToList();

            if (candidates.Count < 2)
            {
                foreach (var fallback in LoadLegacyCandidates(normalizedCode, netTypeCode, inspectionTime, diagnostics))
                {
                    if (candidates.All(item => !TextComparer.Equals(item.ProtocolName, fallback.ProtocolName) ||
                                               !TextComparer.Equals(item.StreamUrl, fallback.StreamUrl)))
                    {
                        candidates.Add(fallback);
                    }
                }
            }

            candidates = candidates
                .Where(item => !string.IsNullOrWhiteSpace(item.StreamUrl))
                .OrderByDescending(item => item.Priority)
                .ThenBy(item => item.Level)
                .ThenBy(item => item.ProtocolName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (candidates.Count == 0)
            {
                return SaveResult(new DeviceInspectionResult
                {
                    DeviceCode = normalizedCode,
                    DeviceName = normalizedName,
                    OnlineStatus = onlineStatus,
                    InspectionTime = inspectionTime,
                    PreferredProtocol = string.Empty,
                    FallbackProtocol = string.Empty,
                    PreferredUrl = string.Empty,
                    FallbackUrl = string.Empty,
                    ExpireTime = null,
                    VideoEnc = streamBundle.VideoEncoding,
                    PlaybackHealthGrade = PlaybackHealthGrade.E,
                    FailureReason = diagnostics.FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                        ?? "设备在线，但未获取到可用直播地址。",
                    Suggestion = "建议检查直播能力开通状态、设备协议兼容性和企业主授权范围后再执行复检。",
                    NeedRecheck = true
                });
            }

            var preferred = candidates[0];
            var fallbackProtocol = candidates.Skip(1).FirstOrDefault();
            var expireTime = ResolveExpireTime(preferred, fallbackProtocol);
            var grade = ResolvePlaybackHealthGrade(preferred, fallbackProtocol, streamBundle.VideoEncoding, expireTime, inspectionTime);
            var failureReason = grade switch
            {
                PlaybackHealthGrade.D => diagnostics.FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                    ?? "当前仅保留单路可播地址，稳定性待复检。",
                PlaybackHealthGrade.E => diagnostics.FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                    ?? "当前不可播放。",
                _ => string.Empty
            };

            return SaveResult(new DeviceInspectionResult
            {
                DeviceCode = normalizedCode,
                DeviceName = normalizedName,
                OnlineStatus = onlineStatus,
                InspectionTime = inspectionTime,
                PreferredProtocol = preferred.ProtocolName,
                FallbackProtocol = fallbackProtocol?.ProtocolName ?? string.Empty,
                PreferredUrl = preferred.StreamUrl,
                FallbackUrl = fallbackProtocol?.StreamUrl ?? string.Empty,
                ExpireTime = expireTime,
                VideoEnc = streamBundle.VideoEncoding,
                PlaybackHealthGrade = grade,
                FailureReason = failureReason,
                Suggestion = BuildSuggestion(grade, preferred.ProtocolName, fallbackProtocol?.ProtocolName, streamBundle.VideoEncoding),
                NeedRecheck = grade is PlaybackHealthGrade.D or PlaybackHealthGrade.E
            });
        }
        catch (PlatformServiceException ex)
        {
            return SaveResult(new DeviceInspectionResult
            {
                DeviceCode = normalizedCode,
                DeviceName = normalizedName,
                OnlineStatus = null,
                InspectionTime = inspectionTime,
                PreferredProtocol = string.Empty,
                FallbackProtocol = string.Empty,
                PreferredUrl = string.Empty,
                FallbackUrl = string.Empty,
                ExpireTime = null,
                VideoEnc = "未知",
                PlaybackHealthGrade = PlaybackHealthGrade.E,
                FailureReason = BuildFailureSummary(ex.Message),
                Suggestion = "建议先确认 accessToken、设备权限和直播能力开通状态，再执行复检。",
                NeedRecheck = true
            });
        }
        catch (Exception ex)
        {
            return SaveResult(new DeviceInspectionResult
            {
                DeviceCode = normalizedCode,
                DeviceName = normalizedName,
                OnlineStatus = null,
                InspectionTime = inspectionTime,
                PreferredProtocol = string.Empty,
                FallbackProtocol = string.Empty,
                PreferredUrl = string.Empty,
                FallbackUrl = string.Empty,
                ExpireTime = null,
                VideoEnc = "未知",
                PlaybackHealthGrade = PlaybackHealthGrade.E,
                FailureReason = BuildFailureSummary(ex.Message),
                Suggestion = "建议检查本地网络、开放平台配置和设备绑定关系后重新巡检。",
                NeedRecheck = true
            });
        }
    }

    private int? QueryOnlineStatus(string deviceCode)
    {
        var response = Execute(DeviceStatusEndpoint, new Dictionary<string, string>
        {
            ["deviceCode"] = deviceCode
        });

        var payload = UnwrapResponseData(response.Data);
        return ReadInt32(payload, "status");
    }

    private StreamBundle TryLoadH5Streams(
        string deviceCode,
        int? netTypeCode,
        DateTimeOffset inspectionTime,
        ICollection<string> diagnostics)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["deviceCode"] = deviceCode,
                ["mute"] = "1",
                ["playerType"] = "1",
                ["wasm"] = "1",
                ["allLiveUrl"] = "1"
            };

            if (netTypeCode is 0 or 1)
            {
                parameters["netType"] = netTypeCode.Value.ToString(CultureInfo.InvariantCulture);
            }

            var response = Execute(H5StreamEndpoint, parameters);
            var payload = UnwrapResponseData(response.Data);
            var expireIn = ReadInt32(payload, "expireIn");
            var videoEncoding = ResolveVideoEncoding(ReadInt32(payload, "videoEnc"));
            var candidates = ReadArray(payload, "streamUrls")
                .Select(item => BuildCandidate(item, expireIn, inspectionTime))
                .Where(item => item is not null)
                .Select(item => item!)
                .ToList();

            return new StreamBundle(candidates, videoEncoding);
        }
        catch (PlatformServiceException ex)
        {
            diagnostics.Add($"H5直播地址接口失败：{BuildFailureSummary(ex.Message)}");
            return new StreamBundle([], "未知");
        }
    }

    private IEnumerable<StreamCandidate> LoadLegacyCandidates(
        string deviceCode,
        int? netTypeCode,
        DateTimeOffset inspectionTime,
        ICollection<string> diagnostics)
    {
        var candidates = new List<StreamCandidate>();

        TryAppendCandidate(
            candidates,
            diagnostics,
            "WebRTC 备用流",
            () => Execute(WebRtcStreamEndpoint, BuildWebRtcParameters(deviceCode, netTypeCode)),
            payload => BuildLegacyCandidate("WebRTC", "webrtc", payload, inspectionTime, 100, 1));

        TryAppendCandidate(
            candidates,
            diagnostics,
            "HLS 备用流",
            () => Execute(HlsStreamEndpoint, BuildHlsParameters(deviceCode, netTypeCode)),
            payload => BuildLegacyCandidate("HLS", "https", payload, inspectionTime, 60, 2));

        TryAppendCandidate(
            candidates,
            diagnostics,
            "FLV 备用流",
            () => Execute(FlvStreamEndpoint, BuildFlvParameters(deviceCode)),
            payload => BuildLegacyCandidate("FLV", "https", payload, inspectionTime, 55, 3));

        return candidates;
    }

    private static Dictionary<string, string> BuildWebRtcParameters(string deviceCode, int? netTypeCode)
    {
        var parameters = new Dictionary<string, string>
        {
            ["deviceCode"] = deviceCode,
            ["channel"] = "0",
            ["mute"] = "1"
        };

        if (netTypeCode is 0 or 1)
        {
            parameters["netType"] = netTypeCode.Value.ToString(CultureInfo.InvariantCulture);
        }

        return parameters;
    }

    private static Dictionary<string, string> BuildHlsParameters(string deviceCode, int? netTypeCode)
    {
        var parameters = new Dictionary<string, string>
        {
            ["deviceCode"] = deviceCode,
            ["supportDomain"] = "1",
            ["mute"] = "1",
            ["expire"] = "600"
        };

        if (netTypeCode is 0 or 1)
        {
            parameters["netType"] = netTypeCode.Value.ToString(CultureInfo.InvariantCulture);
        }

        return parameters;
    }

    private static Dictionary<string, string> BuildFlvParameters(string deviceCode)
    {
        return new Dictionary<string, string>
        {
            ["deviceCode"] = deviceCode,
            ["mute"] = "1"
        };
    }

    private static void TryAppendCandidate(
        ICollection<StreamCandidate> candidates,
        ICollection<string> diagnostics,
        string sourceLabel,
        Func<OpenPlatformResponseEnvelope<JsonElement>> execute,
        Func<JsonElement, StreamCandidate?> map)
    {
        try
        {
            var payload = UnwrapResponseData(execute().Data);
            var candidate = map(payload);
            if (candidate is not null)
            {
                candidates.Add(candidate);
            }
        }
        catch (PlatformServiceException ex)
        {
            diagnostics.Add($"{sourceLabel}失败：{BuildFailureSummary(ex.Message)}");
        }
        catch (Exception ex)
        {
            diagnostics.Add($"{sourceLabel}失败：{BuildFailureSummary(ex.Message)}");
        }
    }

    private static StreamCandidate? BuildCandidate(JsonElement item, int? expireIn, DateTimeOffset inspectionTime)
    {
        var streamUrl = ReadString(item, "streamUrl");
        if (string.IsNullOrWhiteSpace(streamUrl))
        {
            return null;
        }

        var protocolName = ResolveProtocolName(ReadInt32(item, "protocol"), streamUrl);
        var level = ReadInt32(item, "level") ?? 9;
        var expireTime = expireIn.HasValue && expireIn.Value > 0
            ? inspectionTime.AddSeconds(expireIn.Value)
            : (DateTimeOffset?)null;
        var priority = ResolvePriority(protocolName, level);

        return new StreamCandidate(protocolName, streamUrl.Trim(), expireTime, priority, level);
    }

    private static StreamCandidate? BuildLegacyCandidate(
        string protocolName,
        string expectedScheme,
        JsonElement payload,
        DateTimeOffset inspectionTime,
        int priority,
        int level)
    {
        var streamUrl = ReadString(payload, "streamUrl", "url");
        if (string.IsNullOrWhiteSpace(streamUrl))
        {
            return null;
        }

        var normalizedUrl = streamUrl.Trim();
        var resolvedProtocol = ResolveProtocolName(null, normalizedUrl);
        if (!resolvedProtocol.Contains(protocolName, StringComparison.OrdinalIgnoreCase))
        {
            resolvedProtocol = protocolName;
        }

        var expireSeconds = ReadInt32(payload, "expireTime");
        var expireTime = expireSeconds.HasValue && expireSeconds.Value > 0
            ? inspectionTime.AddSeconds(expireSeconds.Value)
            : (DateTimeOffset?)null;

        if (!normalizedUrl.StartsWith(expectedScheme, StringComparison.OrdinalIgnoreCase) &&
            !TextComparer.Equals(protocolName, "WebRTC"))
        {
            resolvedProtocol = $"{protocolName} / 兼容流";
        }

        return new StreamCandidate(resolvedProtocol, normalizedUrl, expireTime, priority, level);
    }

    private static DateTimeOffset? ResolveExpireTime(StreamCandidate preferred, StreamCandidate? fallback)
    {
        if (preferred.ExpireTime is null)
        {
            return fallback?.ExpireTime;
        }

        if (fallback?.ExpireTime is null)
        {
            return preferred.ExpireTime;
        }

        return preferred.ExpireTime <= fallback.ExpireTime
            ? preferred.ExpireTime
            : fallback.ExpireTime;
    }

    private static PlaybackHealthGrade ResolvePlaybackHealthGrade(
        StreamCandidate preferred,
        StreamCandidate? fallback,
        string videoEncoding,
        DateTimeOffset? expireTime,
        DateTimeOffset inspectionTime)
    {
        var hasFallback = fallback is not null;
        var protocol = preferred.ProtocolName;
        var grade = protocol switch
        {
            var value when value.Contains("WebRTC", StringComparison.OrdinalIgnoreCase) && hasFallback && videoEncoding == "H.264" => PlaybackHealthGrade.A,
            var value when value.Contains("WebRTC", StringComparison.OrdinalIgnoreCase) => PlaybackHealthGrade.B,
            var value when value.Contains("WSS-FMP4", StringComparison.OrdinalIgnoreCase) || value.Contains("WSS-FLV", StringComparison.OrdinalIgnoreCase)
                => hasFallback ? PlaybackHealthGrade.B : PlaybackHealthGrade.C,
            var value when value.Contains("HLS", StringComparison.OrdinalIgnoreCase) || value.Contains("FLV", StringComparison.OrdinalIgnoreCase)
                => hasFallback ? PlaybackHealthGrade.C : PlaybackHealthGrade.D,
            _ => hasFallback ? PlaybackHealthGrade.D : PlaybackHealthGrade.E
        };

        if (expireTime.HasValue && expireTime.Value <= inspectionTime.AddMinutes(2))
        {
            grade = Degrade(grade);
        }

        return grade;
    }

    private static PlaybackHealthGrade Degrade(PlaybackHealthGrade grade)
    {
        return grade switch
        {
            PlaybackHealthGrade.A => PlaybackHealthGrade.B,
            PlaybackHealthGrade.B => PlaybackHealthGrade.C,
            PlaybackHealthGrade.C => PlaybackHealthGrade.D,
            PlaybackHealthGrade.D => PlaybackHealthGrade.E,
            _ => PlaybackHealthGrade.E
        };
    }

    private static string BuildSuggestion(
        PlaybackHealthGrade grade,
        string preferredProtocol,
        string? fallbackProtocol,
        string videoEncoding)
    {
        return grade switch
        {
            PlaybackHealthGrade.A => $"建议后续播放器优先尝试 {preferredProtocol}，当前具备秒开链路，可保留 {ValueOrFallback(fallbackProtocol, "备用流")} 作为兜底。",
            PlaybackHealthGrade.B => $"建议优先使用 {preferredProtocol}，播放失败时自动切换到 {ValueOrFallback(fallbackProtocol, "备用流")}。",
            PlaybackHealthGrade.C => $"建议优先走 {preferredProtocol}，并保留 {ValueOrFallback(fallbackProtocol, "备用流")}；当前编码 {videoEncoding}，应安排后续人工复核起播时延。",
            PlaybackHealthGrade.D => $"建议先用 {preferredProtocol} 做临时复核，并尽快安排短周期复检或人工确认播放稳定性。",
            _ => "建议优先排查设备在线状态、直播能力授权、企业主令牌和设备协议兼容性，再重新巡检。"
        };
    }

    private static string BuildOfflineFailureReason(int? onlineStatus)
    {
        return onlineStatus switch
        {
            0 => "设备当前离线，未生成可用直播地址。",
            2 => "设备处于休眠状态，当前不输出直播视频流。",
            3 => "设备处于保活休眠状态，当前直播链路不稳定。",
            -1 => "设备已不在当前企业主账号名下。",
            _ => "未获取到明确在线状态，当前无法判定播放能力。"
        };
    }

    private static string BuildOfflineSuggestion(int? onlineStatus)
    {
        return onlineStatus switch
        {
            -1 => "建议先确认企业主授权范围、设备归属和目录绑定关系，再执行复检。",
            0 or 2 or 3 => "建议先排查设备供电、网络连通性和在线状态恢复情况，再执行复检。",
            _ => "建议先确认设备在线状态和基础授权配置，再执行复检。"
        };
    }

    private static string BuildFailureSummary(string message)
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

    private DeviceInspectionResult SaveResult(DeviceInspectionResult result)
    {
        lock (_syncRoot)
        {
            var existing = _store.Load()
                .Where(item => !TextComparer.Equals(item.DeviceCode, result.DeviceCode))
                .ToList();
            existing.Add(result);

            _store.Save(existing
                .OrderBy(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }

        return result;
    }

    private static string ResolveVideoEncoding(int? videoEnc)
    {
        return videoEnc switch
        {
            0 => "H.264",
            1 => "H.265",
            _ => "未知"
        };
    }

    private static string ResolveProtocolName(int? protocolCode, string streamUrl)
    {
        if (protocolCode == 7)
        {
            return "WebRTC";
        }

        if (protocolCode == 8)
        {
            return "WSS-FMP4";
        }

        if (Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri))
        {
            var scheme = uri.Scheme.ToLowerInvariant();
            var url = streamUrl.ToLowerInvariant();
            return scheme switch
            {
                "webrtc" => "WebRTC",
                "wss" when url.Contains(".flv", StringComparison.Ordinal) => "WSS-FLV",
                "wss" when url.Contains(".mp4", StringComparison.Ordinal) || url.Contains("fmp4", StringComparison.Ordinal) => "WSS-FMP4",
                "https" when url.Contains(".m3u8", StringComparison.Ordinal) => "HLS",
                "http" when url.Contains(".m3u8", StringComparison.Ordinal) => "HLS",
                "https" when url.Contains(".flv", StringComparison.Ordinal) => "FLV",
                "http" when url.Contains(".flv", StringComparison.Ordinal) => "FLV",
                "rtmp" => "RTMP",
                "rtsp" => "RTSP",
                _ => scheme.ToUpperInvariant()
            };
        }

        return protocolCode.HasValue
            ? $"PROTOCOL-{protocolCode.Value}"
            : "UNKNOWN";
    }

    private static int ResolvePriority(string protocolName, int level)
    {
        var basePriority = protocolName switch
        {
            var value when value.Contains("WebRTC", StringComparison.OrdinalIgnoreCase) => 100,
            var value when value.Contains("WSS-FMP4", StringComparison.OrdinalIgnoreCase) => 90,
            var value when value.Contains("WSS-FLV", StringComparison.OrdinalIgnoreCase) => 80,
            var value when value.Contains("HLS", StringComparison.OrdinalIgnoreCase) => 60,
            var value when value.Contains("FLV", StringComparison.OrdinalIgnoreCase) => 55,
            var value when value.Contains("RTMP", StringComparison.OrdinalIgnoreCase) => 40,
            _ => 20
        };

        return basePriority - Math.Max(0, level);
    }

    private static string ValueOrFallback(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private sealed record StreamBundle(IReadOnlyList<StreamCandidate> Candidates, string VideoEncoding);

    private sealed record StreamCandidate(
        string ProtocolName,
        string StreamUrl,
        DateTimeOffset? ExpireTime,
        int Priority,
        int Level);
}
