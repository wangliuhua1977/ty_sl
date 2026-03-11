using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using TylinkInspection.Core.Configuration;
using TylinkInspection.UI.ViewModels;

namespace TylinkInspection.UI.Views.Pages;

public partial class MapInspectionPageView : UserControl
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private MapInspectionPageViewModel? _viewModel;
    private bool _isInitialized;
    private bool _isMapHostReady;

    public MapInspectionPageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await EnsureMapInitializedAsync();
        await PushMapStateAsync();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.MapPoints.CollectionChanged -= OnMapPointsCollectionChanged;
        }

        _viewModel = DataContext as MapInspectionPageViewModel;
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.MapPoints.CollectionChanged += OnMapPointsCollectionChanged;
        _ = EnsureMapInitializedAsync();
        _ = PushMapStateAsync();
    }

    private void OnMapPointsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        _ = PushMapStateAsync();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MapInspectionPageViewModel.SelectedPointDeviceCode) or nameof(MapInspectionPageViewModel.IsPickMode))
        {
            _ = PushMapStateAsync();
        }
    }

    private async Task EnsureMapInitializedAsync()
    {
        if (_isInitialized || _viewModel is null)
        {
            return;
        }

        if (!_viewModel.MapOptions.IsConfigured)
        {
            _viewModel.ReportMapError("未配置高德地图 Key 或安全密钥，请在本地 appsettings.Local.json 的 MapProvider 节点注入配置。");
            return;
        }

        try
        {
            await MapWebView.EnsureCoreWebView2Async();
            MapWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            var runtimeHostPagePath = PrepareRuntimeHostPage(_viewModel.MapOptions);
            MapWebView.Source = new Uri(runtimeHostPagePath);
            _isInitialized = true;
        }
        catch (WebView2RuntimeNotFoundException)
        {
            _viewModel.ReportMapError("当前系统缺少 WebView2 Runtime，地图页无法加载高德地图。");
        }
        catch (Exception ex)
        {
            _viewModel.ReportMapError($"地图初始化失败：{ex.Message}");
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var typeElement))
            {
                return;
            }

            var messageType = typeElement.GetString();
            switch (messageType)
            {
                case "mapReady":
                    _isMapHostReady = true;
                    _viewModel.ReportMapReady();
                    _ = PushMapStateAsync();
                    break;
                case "markerClick":
                    _viewModel.HandleMapPointSelected(ReadString(root, "deviceCode"));
                    break;
                case "mapPick":
                    if (root.TryGetProperty("longitude", out var longitudeElement) &&
                        root.TryGetProperty("latitude", out var latitudeElement))
                    {
                        _viewModel.HandleMapCoordinatePicked(longitudeElement.GetDouble(), latitudeElement.GetDouble());
                    }

                    _ = PushMapStateAsync();
                    break;
                case "error":
                    _viewModel.ReportMapError(ReadString(root, "message") ?? "地图脚本加载失败。");
                    break;
            }
        }
        catch (Exception ex)
        {
            _viewModel.ReportMapError($"地图消息处理失败：{ex.Message}");
        }
    }

    private async Task PushMapStateAsync()
    {
        if (!_isInitialized || !_isMapHostReady || _viewModel is null || MapWebView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            var pointsJson = JsonSerializer.Serialize(_viewModel.MapPoints, JsonOptions);
            var selectedDeviceJson = JsonSerializer.Serialize(_viewModel.HasSelectedPoint ? _viewModel.SelectedPointDeviceCode : null, JsonOptions);
            var pickModeJson = _viewModel.IsPickMode ? "true" : "false";

            await MapWebView.ExecuteScriptAsync($"window.tylinkMap && window.tylinkMap.setPoints({pointsJson});");
            await MapWebView.ExecuteScriptAsync($"window.tylinkMap && window.tylinkMap.setSelectedDevice({selectedDeviceJson});");
            await MapWebView.ExecuteScriptAsync($"window.tylinkMap && window.tylinkMap.setPickMode({pickModeJson});");
        }
        catch (Exception ex)
        {
            _viewModel.ReportMapError($"地图状态同步失败：{ex.Message}");
        }
    }

    private static string PrepareRuntimeHostPage(AmapMapOptions options)
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Map", "amap-host.template.html");
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("未找到地图宿主模板文件。", templatePath);
        }

        var outputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TylinkInspection",
            "runtime");
        Directory.CreateDirectory(outputDirectory);

        var html = File.ReadAllText(templatePath)
            .Replace("__AMAP_KEY__", JsonSerializer.Serialize(options.JsApiKey, JsonOptions), StringComparison.Ordinal)
            .Replace("__AMAP_SECURITY_JS_CODE__", JsonSerializer.Serialize(options.SecurityJsCode, JsonOptions), StringComparison.Ordinal)
            .Replace("__AMAP_VERSION__", JsonSerializer.Serialize(options.JsApiVersion, JsonOptions), StringComparison.Ordinal)
            .Replace("__COORDINATE_SYSTEM__", JsonSerializer.Serialize(options.CoordinateSystem, JsonOptions), StringComparison.Ordinal);

        var outputPath = Path.Combine(outputDirectory, "amap-host.html");
        File.WriteAllText(outputPath, html);
        return outputPath;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property.GetString()
            : null;
    }
}
