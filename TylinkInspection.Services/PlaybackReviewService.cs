using System.Globalization;
using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;
using TylinkInspection.Core.Utilities;

namespace TylinkInspection.Services;

public sealed class PlaybackReviewService : OpenPlatformAlarmServiceBase, IPlaybackReviewService
{
    private const string H5StreamEndpoint = "/open/token/vpaas/getH5StreamUrl";
    private const string WebRtcStreamEndpoint = "/open/token/vpaas/getDeviceMediaWebrtcUrl";
    private const string HlsStreamEndpoint = "/open/token/cloud/getDeviceMediaUrlHls";
    private static readonly StringComparer TextComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IPlaybackReviewStore _store;
    private readonly IDeviceInspectionService _deviceInspectionService;
    private readonly object _syncRoot = new();

    public PlaybackReviewService(
        IOpenPlatformOptionsProvider optionsProvider,
        ITokenService tokenService,
        IOpenPlatformClient openPlatformClient,
        IPlaybackReviewStore store,
        IDeviceInspectionService deviceInspectionService)
        : base(optionsProvider, tokenService, openPlatformClient)
    {
        _store = store;
        _deviceInspectionService = deviceInspectionService;
    }

    public PlaybackReviewSession PrepareLiveReview(PlaybackReviewPreparationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedCode = request.DeviceCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new InvalidOperationException("设备编码不能为空，无法准备直播复核。");
        }

        var normalizedName = string.IsNullOrWhiteSpace(request.DeviceName)
            ? normalizedCode
            : request.DeviceName.Trim();
        var now = DateTimeOffset.Now;
        var diagnostics = new List<string>();
        var freshSources = new List<PlaybackReviewSource>();
        var staleSources = new List<PlaybackReviewSource>();

        var baseInspection = request.BaseInspectionResult
            ?? _deviceInspectionService.GetLatestResult(normalizedCode);

        AppendInspectionSources(baseInspection, freshSources, staleSources, now);

        var browserBundle = StreamBundle.Empty;
        if (request.ForceRefresh || !HasFreshProtocol(freshSources, "WebRTC", now) || !HasFreshProtocol(freshSources, "HLS", now))
        {
            browserBundle = TryLoadOfficialBrowserSources(normalizedCode, request.NetTypeCode, now, diagnostics);
            foreach (var source in browserBundle.Sources)
            {
                AppendSource(source, freshSources, staleSources, now);
            }
        }

        var refreshed = browserBundle.Sources.Count > 0;

        if (request.ForceRefresh || !HasFreshProtocol(freshSources, "WebRTC", now))
        {
            var webRtcSource = TryLoadDirectWebRtc(normalizedCode, request.NetTypeCode, now, diagnostics);
            if (webRtcSource is not null)
            {
                refreshed = true;
                AppendSource(webRtcSource, freshSources, staleSources, now);
            }
        }

        if (request.ForceRefresh || !HasFreshProtocol(freshSources, "HLS", now))
        {
            var hlsSource = TryLoadDirectHls(normalizedCode, request.NetTypeCode, now, diagnostics);
            if (hlsSource is not null)
            {
                refreshed = true;
                AppendSource(hlsSource, freshSources, staleSources, now);
            }
        }

        var orderedSources = OrderSources(freshSources, staleSources);
        var preferredSource = orderedSources.FirstOrDefault();
        var fallbackSource = orderedSources.Skip(1).FirstOrDefault();

        var refreshReason = request.ForceRefresh
            ? "已按请求刷新官方 WebRTC / HLS 复核地址。"
            : refreshed
                ? "已补拉官方 H5 播放地址，并保留可复用的直播复核流。"
                : "已优先复用最近基础巡检结果中的直播地址。";

        return new PlaybackReviewSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            ReviewTargetKind = "Live",
            DeviceCode = normalizedCode,
            DeviceName = normalizedName,
            CreatedAt = now,
            VideoEncoding = ResolveVideoEncoding(baseInspection?.VideoEnc, browserBundle.VideoEncoding),
            PreferredProtocol = preferredSource?.Protocol ?? baseInspection?.PreferredProtocol ?? string.Empty,
            FallbackProtocol = fallbackSource?.Protocol ?? baseInspection?.FallbackProtocol ?? string.Empty,
            PreferredUrl = preferredSource?.Url ?? baseInspection?.PreferredUrl ?? string.Empty,
            FallbackUrl = fallbackSource?.Url ?? baseInspection?.FallbackUrl ?? string.Empty,
            InspectionExpireTime = ResolveSessionExpireTime(baseInspection?.ExpireTime, orderedSources),
            AddressRefreshed = refreshed || request.ForceRefresh,
            RefreshReason = refreshReason,
            DiagnosticMessage = JoinDiagnostics(diagnostics),
            Sources = orderedSources
        };
    }

    public PlaybackReviewSession PreparePlaybackReview(PlaybackReviewPlaybackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedCode = request.DeviceCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new InvalidOperationException("设备编码不能为空，无法准备回看复核。");
        }

        if (string.IsNullOrWhiteSpace(request.PlaybackFileId))
        {
            throw new InvalidOperationException("回看文件标识不能为空，无法准备回看复核。");
        }

        var normalizedName = string.IsNullOrWhiteSpace(request.DeviceName)
            ? normalizedCode
            : request.DeviceName.Trim();
        var sources = new List<PlaybackReviewSource>();
        var diagnostics = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.HlsStreamUrl))
        {
            sources.Add(new PlaybackReviewSource
            {
                Protocol = "HLS",
                Url = request.HlsStreamUrl.Trim(),
                ExpireTime = null,
                SourceCategory = "CloudPlaybackHls",
                IsFallback = false
            });
        }

        if (!string.IsNullOrWhiteSpace(request.RtmpStreamUrl))
        {
            sources.Add(new PlaybackReviewSource
            {
                Protocol = "RTMP",
                Url = request.RtmpStreamUrl.Trim(),
                ExpireTime = null,
                SourceCategory = "CloudPlaybackRtmp",
                IsFallback = sources.Count > 0
            });
        }

        if (sources.Count == 0)
        {
            diagnostics.Add("当前未获取到可用于回看复核的流化地址。");
        }
        else if (!string.IsNullOrWhiteSpace(request.RtmpStreamUrl) && string.IsNullOrWhiteSpace(request.HlsStreamUrl))
        {
            diagnostics.Add("当前仅获取到 RTMP 回看地址，浏览器宿主会尝试但不保证可播。");
        }

        if (!string.IsNullOrWhiteSpace(request.DiagnosticMessage))
        {
            diagnostics.Add(request.DiagnosticMessage.Trim());
        }

        var orderedSources = sources
            .OrderByDescending(GetProtocolPriority)
            .Select((source, index) => CloneWithFallback(source, index > 0))
            .ToList();

        var preferredSource = orderedSources.FirstOrDefault();
        var fallbackSource = orderedSources.Skip(1).FirstOrDefault();

        return new PlaybackReviewSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            ReviewTargetKind = "Playback",
            DeviceCode = normalizedCode,
            DeviceName = normalizedName,
            PlaybackFileId = request.PlaybackFileId.Trim(),
            PlaybackFileName = string.IsNullOrWhiteSpace(request.PlaybackFileName)
                ? request.PlaybackFileId.Trim()
                : request.PlaybackFileName.Trim(),
            CreatedAt = DateTimeOffset.Now,
            VideoEncoding = NormalizeText(request.VideoEncoding, "未知"),
            PreferredProtocol = preferredSource?.Protocol ?? string.Empty,
            FallbackProtocol = fallbackSource?.Protocol ?? string.Empty,
            PreferredUrl = preferredSource?.Url ?? string.Empty,
            FallbackUrl = fallbackSource?.Url ?? string.Empty,
            InspectionExpireTime = null,
            AddressRefreshed = request.AddressRefreshed,
            RefreshReason = request.AddressRefreshed
                ? "已刷新回看流化地址。"
                : "已复用最近回看流化地址。",
            DiagnosticMessage = JoinDiagnostics(diagnostics),
            Sources = orderedSources
        };
    }

    public PlaybackReviewResult CompleteReview(PlaybackReviewOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var result = new PlaybackReviewResult
        {
            SessionId = outcome.SessionId.Trim(),
            ReviewTargetKind = NormalizeText(outcome.ReviewTargetKind, "Live"),
            DeviceCode = outcome.DeviceCode.Trim(),
            DeviceName = string.IsNullOrWhiteSpace(outcome.DeviceName)
                ? outcome.DeviceCode.Trim()
                : outcome.DeviceName.Trim(),
            PlaybackFileId = outcome.PlaybackFileId.Trim(),
            PlaybackFileName = outcome.PlaybackFileName.Trim(),
            ReviewedAt = outcome.ReviewedAt,
            PlaybackStarted = outcome.PlaybackStarted,
            FirstFrameVisible = outcome.FirstFrameVisible,
            StartupDurationMs = outcome.StartupDurationMs is > 0 ? outcome.StartupDurationMs : null,
            UsedProtocol = outcome.UsedProtocol.Trim(),
            UsedUrl = SensitiveDataMasker.MaskUrl(outcome.UsedUrl),
            UsedFallback = outcome.UsedFallback,
            FailureReason = outcome.FailureReason.Trim(),
            VideoEncoding = outcome.VideoEncoding.Trim()
        };

        lock (_syncRoot)
        {
            var items = _store.Load()
                .Where(item => !TextComparer.Equals(item.SessionId, result.SessionId))
                .ToList();
            items.Add(result);

            _store.Save(items
                .OrderBy(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(item => item.ReviewedAt)
                .ToList());
        }

        return result;
    }

    public PlaybackReviewResult? GetLatestResult(string deviceCode)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            return null;
        }

        lock (_syncRoot)
        {
            return _store.Load()
                .Where(item => TextComparer.Equals(item.DeviceCode, deviceCode.Trim()))
                .OrderByDescending(item => item.ReviewedAt)
                .FirstOrDefault();
        }
    }

    private static void AppendInspectionSources(
        DeviceInspectionResult? inspection,
        ICollection<PlaybackReviewSource> freshSources,
        ICollection<PlaybackReviewSource> staleSources,
        DateTimeOffset now)
    {
        if (inspection is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(inspection.PreferredUrl))
        {
            AppendSource(new PlaybackReviewSource
            {
                Protocol = NormalizeInspectionProtocol(inspection.PreferredProtocol),
                Url = inspection.PreferredUrl,
                ExpireTime = inspection.ExpireTime,
                SourceCategory = "InspectionPreferred",
                IsFallback = false
            }, freshSources, staleSources, now);
        }

        if (!string.IsNullOrWhiteSpace(inspection.FallbackUrl))
        {
            AppendSource(new PlaybackReviewSource
            {
                Protocol = NormalizeInspectionProtocol(inspection.FallbackProtocol),
                Url = inspection.FallbackUrl,
                ExpireTime = inspection.ExpireTime,
                SourceCategory = "InspectionFallback",
                IsFallback = true
            }, freshSources, staleSources, now);
        }
    }

    private StreamBundle TryLoadOfficialBrowserSources(
        string deviceCode,
        int? netTypeCode,
        DateTimeOffset now,
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

            var payload = UnwrapResponseData(Execute(H5StreamEndpoint, parameters).Data);
            var expireIn = ReadInt32(payload, "expireIn");
            var expireTime = expireIn is > 0 ? now.AddSeconds(expireIn.Value) : (DateTimeOffset?)null;
            var videoEncoding = ResolveVideoEncoding(ReadInt32(payload, "videoEnc"));

            var sources = ReadArray(payload, "streamUrls")
                .Select(item => BuildOfficialBrowserSource(item, expireTime))
                .Where(item => item is not null)
                .Select(item => item!)
                .OrderByDescending(GetProtocolPriority)
                .ToList();

            return new StreamBundle(sources, videoEncoding);
        }
        catch (PlatformServiceException ex)
        {
            diagnostics.Add($"官方 H5 直播地址刷新失败：{SummarizeFailure(ex.Message)}");
            return StreamBundle.Empty;
        }
        catch (Exception ex)
        {
            diagnostics.Add($"官方 H5 直播地址刷新失败：{SummarizeFailure(ex.Message)}");
            return StreamBundle.Empty;
        }
    }

    private PlaybackReviewSource? TryLoadDirectWebRtc(
        string deviceCode,
        int? netTypeCode,
        DateTimeOffset now,
        ICollection<string> diagnostics)
    {
        try
        {
            var payload = UnwrapResponseData(Execute(WebRtcStreamEndpoint, BuildWebRtcParameters(deviceCode, netTypeCode)).Data);
            return BuildLegacySource("WebRTC", payload, now, "DirectWebRtc");
        }
        catch (PlatformServiceException ex)
        {
            diagnostics.Add($"WebRTC 地址刷新失败：{SummarizeFailure(ex.Message)}");
            return null;
        }
        catch (Exception ex)
        {
            diagnostics.Add($"WebRTC 地址刷新失败：{SummarizeFailure(ex.Message)}");
            return null;
        }
    }

    private PlaybackReviewSource? TryLoadDirectHls(
        string deviceCode,
        int? netTypeCode,
        DateTimeOffset now,
        ICollection<string> diagnostics)
    {
        try
        {
            var payload = UnwrapResponseData(Execute(HlsStreamEndpoint, BuildHlsParameters(deviceCode, netTypeCode)).Data);
            return BuildLegacySource("HLS", payload, now, "DirectHls");
        }
        catch (PlatformServiceException ex)
        {
            diagnostics.Add($"HLS 地址刷新失败：{SummarizeFailure(ex.Message)}");
            return null;
        }
        catch (Exception ex)
        {
            diagnostics.Add($"HLS 地址刷新失败：{SummarizeFailure(ex.Message)}");
            return null;
        }
    }

    private static PlaybackReviewSource? BuildOfficialBrowserSource(JsonElement item, DateTimeOffset? expireTime)
    {
        var streamUrl = ReadString(item, "streamUrl");
        if (string.IsNullOrWhiteSpace(streamUrl))
        {
            return null;
        }

        var protocol = ResolveOfficialProtocol(ReadInt32(item, "protocol"), streamUrl);
        if (string.IsNullOrWhiteSpace(protocol))
        {
            return null;
        }

        return new PlaybackReviewSource
        {
            Protocol = protocol,
            Url = streamUrl.Trim(),
            ExpireTime = expireTime,
            SourceCategory = "OfficialH5",
            IsFallback = !protocol.Contains("WebRTC", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static PlaybackReviewSource? BuildLegacySource(
        string protocol,
        JsonElement payload,
        DateTimeOffset now,
        string sourceCategory)
    {
        var url = ReadString(payload, "streamUrl", "url");
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var expireSeconds = ReadInt32(payload, "expireTime");
        var expireTime = expireSeconds is > 0
            ? now.AddSeconds(expireSeconds.Value)
            : (DateTimeOffset?)null;

        return new PlaybackReviewSource
        {
            Protocol = protocol,
            Url = url.Trim(),
            ExpireTime = expireTime,
            SourceCategory = sourceCategory,
            IsFallback = !protocol.Contains("WebRTC", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static IReadOnlyList<PlaybackReviewSource> OrderSources(
        IEnumerable<PlaybackReviewSource> freshSources,
        IEnumerable<PlaybackReviewSource> staleSources)
    {
        return freshSources
            .Concat(staleSources)
            .OrderByDescending(GetSourcePriority)
            .GroupBy(item => NormalizeProtocol(item.Protocol), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(item => GetProtocolPriority(item.Protocol))
            .Select((source, index) => CloneWithFallback(source, index > 0))
            .ToList();
    }

    private static void AppendSource(
        PlaybackReviewSource source,
        ICollection<PlaybackReviewSource> freshSources,
        ICollection<PlaybackReviewSource> staleSources,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(source.Url))
        {
            return;
        }

        if (freshSources.Any(item => SameSource(item, source)) ||
            staleSources.Any(item => SameSource(item, source)))
        {
            return;
        }

        var isStale = source.ExpireTime.HasValue && source.ExpireTime.Value <= now.AddSeconds(45);
        if (isStale)
        {
            staleSources.Add(source);
            return;
        }

        freshSources.Add(source);
    }

    private static bool HasFreshProtocol(IEnumerable<PlaybackReviewSource> sources, string protocol, DateTimeOffset now)
    {
        return sources.Any(item =>
            item.Protocol.Contains(protocol, StringComparison.OrdinalIgnoreCase) &&
            (!item.ExpireTime.HasValue || item.ExpireTime.Value > now.AddSeconds(45)));
    }

    private static bool SameSource(PlaybackReviewSource left, PlaybackReviewSource right)
    {
        return TextComparer.Equals(left.Protocol, right.Protocol) &&
               TextComparer.Equals(left.Url, right.Url);
    }

    private static PlaybackReviewSource CloneWithFallback(PlaybackReviewSource source, bool isFallback)
    {
        return new PlaybackReviewSource
        {
            Protocol = source.Protocol,
            Url = source.Url,
            ExpireTime = source.ExpireTime,
            SourceCategory = source.SourceCategory,
            IsFallback = isFallback
        };
    }

    private static DateTimeOffset? ResolveSessionExpireTime(DateTimeOffset? inspectionExpireTime, IReadOnlyList<PlaybackReviewSource> sources)
    {
        var sourceExpireTime = sources
            .Where(item => item.ExpireTime.HasValue)
            .Select(item => item.ExpireTime)
            .OrderBy(item => item)
            .FirstOrDefault();

        if (inspectionExpireTime is null)
        {
            return sourceExpireTime;
        }

        if (sourceExpireTime is null)
        {
            return inspectionExpireTime;
        }

        return inspectionExpireTime <= sourceExpireTime
            ? inspectionExpireTime
            : sourceExpireTime;
    }

    private static int GetProtocolPriority(PlaybackReviewSource source)
    {
        return GetProtocolPriority(source.Protocol);
    }

    private static int GetSourcePriority(PlaybackReviewSource source)
    {
        return (GetProtocolPriority(source.Protocol) * 10) + GetSourceCategoryBonus(source.SourceCategory);
    }

    private static int GetProtocolPriority(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol))
        {
            return 0;
        }

        return protocol switch
        {
            var value when value.Contains("WebRTC", StringComparison.OrdinalIgnoreCase) => 100,
            var value when value.Contains("HLS", StringComparison.OrdinalIgnoreCase) => 80,
            var value when value.Contains("FLV", StringComparison.OrdinalIgnoreCase) => 60,
            var value when value.Contains("RTMP", StringComparison.OrdinalIgnoreCase) => 40,
            var value when value.Contains("RTSP", StringComparison.OrdinalIgnoreCase) => 30,
            _ => 10
        };
    }

    private static int GetSourceCategoryBonus(string? sourceCategory)
    {
        if (string.IsNullOrWhiteSpace(sourceCategory))
        {
            return 0;
        }

        return sourceCategory switch
        {
            var value when value.Contains("OfficialH5", StringComparison.OrdinalIgnoreCase) => 3,
            var value when value.Contains("Inspection", StringComparison.OrdinalIgnoreCase) => 2,
            _ => 1
        };
    }

    private static string NormalizeProtocol(string? protocol)
    {
        return string.IsNullOrWhiteSpace(protocol)
            ? string.Empty
            : protocol.Trim().ToUpperInvariant();
    }

    private static string ResolveOfficialProtocol(int? protocolCode, string streamUrl)
    {
        if (protocolCode == 7)
        {
            return "WebRTC";
        }

        if (protocolCode == 9)
        {
            return "FLV";
        }

        if (protocolCode == 10)
        {
            return "HLS";
        }

        if (streamUrl.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
        {
            return "HLS";
        }

        if (streamUrl.Contains(".flv", StringComparison.OrdinalIgnoreCase))
        {
            return "FLV";
        }

        if (streamUrl.StartsWith("webrtc://", StringComparison.OrdinalIgnoreCase))
        {
            return "WebRTC";
        }

        return string.Empty;
    }

    private static string NormalizeInspectionProtocol(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol))
        {
            return string.Empty;
        }

        if (protocol.Contains("HLS", StringComparison.OrdinalIgnoreCase))
        {
            return "HLS";
        }

        if (protocol.Contains("WebRTC", StringComparison.OrdinalIgnoreCase))
        {
            return "WebRTC";
        }

        if (protocol.Contains("FLV", StringComparison.OrdinalIgnoreCase))
        {
            return "FLV";
        }

        if (protocol.Contains("RTMP", StringComparison.OrdinalIgnoreCase))
        {
            return "RTMP";
        }

        if (protocol.Contains("RTSP", StringComparison.OrdinalIgnoreCase))
        {
            return "RTSP";
        }

        return protocol.Trim();
    }

    private static string ResolveVideoEncoding(string? primary, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary) && !string.Equals(primary, "未知", StringComparison.OrdinalIgnoreCase))
        {
            return primary.Trim();
        }

        return NormalizeText(fallback, "未知");
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

    private static Dictionary<string, string> BuildWebRtcParameters(string deviceCode, int? netTypeCode)
    {
        var parameters = new Dictionary<string, string>
        {
            ["deviceCode"] = deviceCode,
            ["channel"] = "0",
            ["mute"] = "1",
            ["expire"] = "600"
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

    private static string JoinDiagnostics(IEnumerable<string> diagnostics)
    {
        return string.Join(" ", diagnostics
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal));
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

    private sealed record StreamBundle(IReadOnlyList<PlaybackReviewSource> Sources, string VideoEncoding)
    {
        public static StreamBundle Empty { get; } = new([], "未知");
    }
}
