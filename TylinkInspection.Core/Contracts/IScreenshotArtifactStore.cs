namespace TylinkInspection.Core.Contracts;

public interface IScreenshotArtifactStore
{
    string SavePng(string deviceCode, DateTimeOffset capturedAt, byte[] imageBytes);
}
