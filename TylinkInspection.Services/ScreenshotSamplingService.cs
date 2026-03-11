using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;
using TylinkInspection.Core.Utilities;

namespace TylinkInspection.Services;

public sealed class ScreenshotSamplingService : IScreenshotSamplingService
{
    private readonly IScreenshotSampleStore _store;
    private readonly IScreenshotArtifactStore _artifactStore;
    private readonly object _syncRoot = new();

    public ScreenshotSamplingService(
        IScreenshotSampleStore store,
        IScreenshotArtifactStore artifactStore)
    {
        _store = store;
        _artifactStore = artifactStore;
    }

    public ScreenshotSampleResult SaveSample(ScreenshotSampleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.DeviceCode))
        {
            throw new InvalidOperationException("设备编码不能为空，无法保存截图样本。");
        }

        var filePath = _artifactStore.SavePng(request.DeviceCode, request.CapturedAt, request.ImageBytes);
        var result = new ScreenshotSampleResult
        {
            SampleId = Guid.NewGuid().ToString("N"),
            ReviewSessionId = request.ReviewSessionId.Trim(),
            ReviewTargetKind = NormalizeText(request.ReviewTargetKind, "Live"),
            DeviceCode = request.DeviceCode.Trim(),
            DeviceName = string.IsNullOrWhiteSpace(request.DeviceName)
                ? request.DeviceCode.Trim()
                : request.DeviceName.Trim(),
            PlaybackFileName = request.PlaybackFileName.Trim(),
            Protocol = request.Protocol.Trim(),
            SourceUrl = SensitiveDataMasker.MaskUrl(request.SourceUrl),
            CapturedAt = request.CapturedAt,
            ImagePath = filePath
        };

        lock (_syncRoot)
        {
            var items = _store.Load().ToList();
            items.Add(result);

            _store.Save(items
                .OrderBy(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(item => item.CapturedAt)
                .ToList());
        }

        return result;
    }

    public ScreenshotSampleResult? GetLatestSample(string deviceCode)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            return null;
        }

        lock (_syncRoot)
        {
            return _store.Load()
                .Where(item => string.Equals(item.DeviceCode, deviceCode.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.CapturedAt)
                .FirstOrDefault();
        }
    }

    private static string NormalizeText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
