using System.Globalization;
using System.Text;
using TylinkInspection.Core.Contracts;

namespace TylinkInspection.Infrastructure.Storage;

public sealed class LocalScreenshotArtifactStore : IScreenshotArtifactStore
{
    private readonly string _rootDirectory;

    public LocalScreenshotArtifactStore(string? rootDirectory = null)
    {
        _rootDirectory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TylinkInspection",
            "screenshot-samples");
    }

    public string SavePng(string deviceCode, DateTimeOffset capturedAt, byte[] imageBytes)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            throw new InvalidOperationException("设备编码不能为空，无法保存截图样本。");
        }

        if (imageBytes.Length == 0)
        {
            throw new InvalidOperationException("截图内容为空，无法保存截图样本。");
        }

        var safeDeviceCode = SanitizeFileSegment(deviceCode);
        var directory = Path.Combine(_rootDirectory, safeDeviceCode, capturedAt.ToLocalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);

        var fileName = $"{safeDeviceCode}_{capturedAt.ToLocalTime():yyyyMMdd_HHmmss_fff}.png";
        var filePath = Path.Combine(directory, fileName);
        File.WriteAllBytes(filePath, imageBytes);
        return filePath;
    }

    private static string SanitizeFileSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            builder.Append(invalidCharacters.Contains(character) ? '_' : character);
        }

        return string.IsNullOrWhiteSpace(builder.ToString())
            ? "device"
            : builder.ToString();
    }
}
