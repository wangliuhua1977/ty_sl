using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IScreenshotSampleStore
{
    IReadOnlyList<ScreenshotSampleResult> Load();

    void Save(IReadOnlyList<ScreenshotSampleResult> results);
}
