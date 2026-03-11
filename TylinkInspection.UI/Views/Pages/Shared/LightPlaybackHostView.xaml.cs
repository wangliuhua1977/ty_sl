using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using TylinkInspection.UI.ViewModels;

namespace TylinkInspection.UI.Views.Pages.Shared;

public partial class LightPlaybackHostView : UserControl
{
    private static readonly HttpClient SignalingHttpClient = CreateHttpClient();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private DeviceMediaReviewViewModel? _viewModel;
    private bool _isInitialized;
    private bool _isHostReady;

    public LightPlaybackHostView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await EnsurePlayerInitializedAsync();
        await PushPlayerStateAsync();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as DeviceMediaReviewViewModel;
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _ = EnsurePlayerInitializedAsync();
        _ = PushPlayerStateAsync();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DeviceMediaReviewViewModel.CurrentSession))
        {
            _ = PushPlayerStateAsync();
        }
    }

    private async Task EnsurePlayerInitializedAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        try
        {
            await PlayerWebView.EnsureCoreWebView2Async();
            PlayerWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            PlayerWebView.Source = new Uri(GetHostPagePath());
            _isInitialized = true;
        }
        catch (WebView2RuntimeNotFoundException)
        {
            _viewModel?.ReportHostMessage(_viewModel.CurrentSession?.SessionId ?? string.Empty, "当前系统缺少 WebView2 Runtime，轻量播放器宿主无法加载。");
        }
        catch (Exception ex)
        {
            _viewModel?.ReportHostMessage(_viewModel.CurrentSession?.SessionId ?? string.Empty, $"轻量播放器宿主初始化失败：{ex.Message}");
        }
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            var messageType = ReadString(root, "type");
            switch (messageType)
            {
                case "hostReady":
                    _isHostReady = true;
                    await PushPlayerStateAsync();
                    break;
                case "status":
                    _viewModel.ReportHostMessage(ReadString(root, "sessionId"), ReadString(root, "message"));
                    break;
                case "attempt":
                    _viewModel.ReportPlaybackAttempted(
                        ReadString(root, "sessionId"),
                        ReadString(root, "protocol"),
                        ReadString(root, "url"),
                        ReadBoolean(root, "usedFallback"));
                    break;
                case "started":
                    _viewModel.ReportPlaybackStarted(
                        ReadString(root, "sessionId"),
                        ReadString(root, "protocol"),
                        ReadString(root, "url"),
                        ReadBoolean(root, "usedFallback"));
                    break;
                case "firstFrame":
                    {
                        var sessionId = ReadString(root, "sessionId");
                        var protocol = ReadString(root, "protocol");
                        var url = ReadString(root, "url");
                        var usedFallback = ReadBoolean(root, "usedFallback");
                        var startupDurationMs = ReadInt32(root, "startupDurationMs");

                        _viewModel.ReportFirstFrameVisible(sessionId, protocol, url, usedFallback, startupDurationMs);
                        _ = CaptureAndSaveScreenshotAsync(sessionId, protocol, url);
                    }
                    break;
                case "finalFailure":
                    _viewModel.ReportPlaybackFailed(
                        ReadString(root, "sessionId"),
                        ReadString(root, "protocol"),
                        ReadString(root, "url"),
                        ReadBoolean(root, "usedFallback"),
                        ReadString(root, "reason"));
                    break;
                case "webrtcSignalRequest":
                    await HandleWebRtcSignalRequestAsync(root);
                    break;
            }
        }
        catch (Exception ex)
        {
            _viewModel.ReportHostMessage(_viewModel.CurrentSession?.SessionId ?? string.Empty, $"播放器宿主消息处理失败：{ex.Message}");
        }
    }

    private async Task HandleWebRtcSignalRequestAsync(JsonElement root)
    {
        var requestId = ReadString(root, "requestId");
        var sessionId = ReadString(root, "sessionId");
        var sourceUrl = ReadString(root, "url");
        var offerSdp = ReadString(root, "offerSdp");

        if (PlayerWebView.CoreWebView2 is null || string.IsNullOrWhiteSpace(requestId))
        {
            return;
        }

        try
        {
            var signaling = await ResolveWebRtcAnswerAsync(sourceUrl, offerSdp);
            PostHostMessage(new
            {
                type = "webrtcSignalResponse",
                requestId,
                sessionId,
                ok = true,
                answerSdp = signaling.AnswerSdp,
                endpoint = signaling.Endpoint
            });
        }
        catch (Exception ex)
        {
            PostHostMessage(new
            {
                type = "webrtcSignalResponse",
                requestId,
                sessionId,
                ok = false,
                reason = ex.Message
            });
        }
    }

    private async Task<WebRtcSignalResult> ResolveWebRtcAnswerAsync(string sourceUrl, string offerSdp)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl) || !sourceUrl.StartsWith("webrtc://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("当前 WebRTC 地址无效，无法发起信令协商。");
        }

        if (string.IsNullOrWhiteSpace(offerSdp))
        {
            throw new InvalidOperationException("当前 WebRTC offer 为空，无法发起信令协商。");
        }

        var candidates = BuildSignalEndpoints(sourceUrl);
        var failures = new List<string>();

        foreach (var endpoint in candidates)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new
                        {
                            api = endpoint,
                            streamurl = sourceUrl,
                            clientip = (string?)null,
                            sdp = offerSdp
                        }, JsonOptions),
                        Encoding.UTF8,
                        "application/json")
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using var response = await SignalingHttpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    failures.Add($"{endpoint} HTTP {(int)response.StatusCode}");
                    continue;
                }

                var answerSdp = TryExtractAnswerSdp(body);
                if (!string.IsNullOrWhiteSpace(answerSdp))
                {
                    return new WebRtcSignalResult(endpoint, answerSdp);
                }

                failures.Add($"{endpoint} 未返回 answer SDP");
            }
            catch (Exception ex)
            {
                failures.Add($"{endpoint} {ex.Message}");
            }
        }

        throw new InvalidOperationException($"WebRTC 信令协商失败：{string.Join(" / ", failures)}");
    }

    private async Task PushPlayerStateAsync()
    {
        if (!_isInitialized || !_isHostReady || PlayerWebView.CoreWebView2 is null)
        {
            return;
        }

        var sessionJson = JsonSerializer.Serialize(_viewModel?.CurrentSession, JsonOptions);
        await PlayerWebView.ExecuteScriptAsync($"window.tylinkPlayer && window.tylinkPlayer.setSession({sessionJson});");
    }

    private async Task CaptureAndSaveScreenshotAsync(string sessionId, string protocol, string url)
    {
        if (_viewModel is null || PlayerWebView.CoreWebView2 is null)
        {
            return;
        }

        using var stream = new MemoryStream();
        await PlayerWebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
        await _viewModel.SaveScreenshotAsync(sessionId, protocol, url, stream.ToArray());
    }

    private void PostHostMessage(object payload)
    {
        if (PlayerWebView.CoreWebView2 is null)
        {
            return;
        }

        PlayerWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static IReadOnlyList<string> BuildSignalEndpoints(string sourceUrl)
    {
        var uri = new Uri(sourceUrl);
        var authority = $"https://{uri.Authority}";
        var query = uri.Query;
        var pathSegments = uri.AbsolutePath.Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        var rootSegment = pathSegments.FirstOrDefault();
        var endpoints = new List<string>();

        if (!string.IsNullOrWhiteSpace(rootSegment))
        {
            endpoints.Add(AppendQuery($"{authority}/{rootSegment}/rtc/v1/play/", query));
            endpoints.Add(AppendQuery($"{authority}/{rootSegment}/rtc/v1/play", query));
        }

        endpoints.Add(AppendQuery($"{authority}/rtc/v1/play/", query));
        endpoints.Add(AppendQuery($"{authority}/rtc/v1/play", query));

        return endpoints
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string AppendQuery(string endpoint, string query)
    {
        return string.IsNullOrWhiteSpace(query)
            ? endpoint
            : $"{endpoint}{query}";
    }

    private static string? TryExtractAnswerSdp(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var trimmed = body.Trim();
        if (trimmed.StartsWith("v=0", StringComparison.Ordinal))
        {
            return trimmed;
        }

        if (!(trimmed.StartsWith('{') && trimmed.EndsWith('}')))
        {
            return null;
        }

        using var document = JsonDocument.Parse(trimmed);
        var root = document.RootElement;
        var answerSdp = ReadString(root, "sdp");
        return string.IsNullOrWhiteSpace(answerSdp)
            ? null
            : answerSdp;
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    private static string GetHostPagePath()
    {
        var hostPagePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Playback", "light-player-host.html");
        if (!File.Exists(hostPagePath))
        {
            throw new FileNotFoundException("未找到轻量播放器宿主页面。", hostPagePath);
        }

        return hostPagePath;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool ReadBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var value) => value,
            _ => false
        };
    }

    private static int ReadInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        return property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out number)
            ? number
            : 0;
    }

    private sealed record WebRtcSignalResult(string Endpoint, string AnswerSdp);
}
