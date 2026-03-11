using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IScreenshotSamplingService
{
    ScreenshotSampleResult SaveSample(ScreenshotSampleRequest request);

    ScreenshotSampleResult? GetLatestSample(string deviceCode);
}
