using System.Text.Json;
using TylinkInspection.Core.Configuration;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;
using TylinkInspection.Core.Utilities;

namespace TylinkInspection.Infrastructure.Configuration;

public sealed class JsonOpenPlatformOptionsProvider : IOpenPlatformOptionsProvider
{
    private const string SectionName = "OpenPlatform";
    private const string LegacySectionName = "TylinkApi";
    private const string SharedSettingsFileName = "appsettings.json";
    private const string LocalSettingsFileName = "appsettings.Local.json";
    private const string LowercaseLocalSettingsFileName = "appsettings.local.json";
    private const string AppProjectFileName = "TylinkInspection.App.csproj";

    private readonly string _runtimeDirectory;
    private readonly string? _preferredSourceDirectory;

    public JsonOpenPlatformOptionsProvider(string? runtimeDirectory = null, string? preferredSourceDirectory = null)
    {
        _runtimeDirectory = NormalizeDirectory(runtimeDirectory ?? AppContext.BaseDirectory);
        _preferredSourceDirectory = string.IsNullOrWhiteSpace(preferredSourceDirectory)
            ? FindProjectDirectory(_runtimeDirectory)
            : NormalizeDirectory(preferredSourceDirectory);
    }

    public TylinkOpenPlatformOptions GetOptions()
    {
        var recommendedSettingsDirectory = _preferredSourceDirectory ?? _runtimeDirectory;
        var sharedSettingsSource = LoadSettingsSection(EnumerateSharedSettingsPaths());
        var localSettingsSource = LoadSettingsSection(EnumerateLocalSettingsPaths());
        var configurationRoot = ResolveConfigurationRoot(sharedSettingsSource?.Path, localSettingsSource?.Path, recommendedSettingsDirectory);

        return new TylinkOpenPlatformOptions
        {
            BaseUrl = ReadString(localSettingsSource?.Section, sharedSettingsSource?.Section, "BaseUrl", "https://vcp.21cn.com"),
            AppId = ReadString(localSettingsSource?.Section, sharedSettingsSource?.Section, "AppId", "your-app-id"),
            AppSecret = ReadString(localSettingsSource?.Section, sharedSettingsSource?.Section, "AppSecret", "your-app-secret"),
            RsaPrivateKey = ReadString(localSettingsSource?.Section, sharedSettingsSource?.Section, "RsaPrivateKey", "your-rsa-private-key"),
            ClientType = ReadString(localSettingsSource?.Section, sharedSettingsSource?.Section, "ClientType", "4"),
            Version = ReadString(localSettingsSource?.Section, sharedSettingsSource?.Section, "Version", OpenPlatformVersionPolicy.CurrentVersion),
            ApiVersion = ReadString(localSettingsSource?.Section, sharedSettingsSource?.Section, "ApiVersion", OpenPlatformVersionPolicy.ApiVersion),
            GrantType = ReadString(localSettingsSource?.Section, sharedSettingsSource?.Section, "GrantType", OpenPlatformTokenGrantTypes.UserUnaware),
            EnterpriseUser = ReadString(localSettingsSource?.Section, sharedSettingsSource?.Section, "EnterpriseUser", "your-enterprise-user"),
            ParentUser = ReadOptionalString(localSettingsSource?.Section, sharedSettingsSource?.Section, "ParentUser"),
            ConfigurationRootPath = configurationRoot,
            RuntimeSettingsDirectoryPath = _runtimeDirectory,
            RecommendedSettingsDirectoryPath = recommendedSettingsDirectory,
            ConfigurationSourceSummary = BuildConfigurationSourceSummary(sharedSettingsSource, localSettingsSource),
            SharedSettingsPath = sharedSettingsSource?.Path ?? string.Empty,
            LocalSettingsPath = localSettingsSource?.Path,
            IsLocalSettingsLoaded = localSettingsSource is not null
        };
    }

    private IEnumerable<string> EnumerateSharedSettingsPaths()
    {
        foreach (var candidateDirectory in EnumerateCandidateDirectories())
        {
            yield return Path.Combine(candidateDirectory, SharedSettingsFileName);
        }
    }

    private IEnumerable<string> EnumerateLocalSettingsPaths()
    {
        if (!string.IsNullOrWhiteSpace(_preferredSourceDirectory))
        {
            yield return Path.Combine(_preferredSourceDirectory, LocalSettingsFileName);
            yield return Path.Combine(_preferredSourceDirectory, LowercaseLocalSettingsFileName);
            yield break;
        }

        yield return Path.Combine(_runtimeDirectory, LocalSettingsFileName);
        yield return Path.Combine(_runtimeDirectory, LowercaseLocalSettingsFileName);
    }

    private IEnumerable<string> EnumerateCandidateDirectories()
    {
        if (!string.IsNullOrWhiteSpace(_preferredSourceDirectory))
        {
            yield return _preferredSourceDirectory;
        }

        if (!string.Equals(_runtimeDirectory, _preferredSourceDirectory, StringComparison.OrdinalIgnoreCase))
        {
            yield return _runtimeDirectory;
        }
    }

    private static SettingsSection? LoadSettingsSection(IEnumerable<string> candidatePaths)
    {
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidatePath in candidatePaths)
        {
            if (!seenPaths.Add(candidatePath))
            {
                continue;
            }

            var section = LoadSettingsSection(candidatePath);
            if (section is not null)
            {
                return section;
            }
        }

        return null;
    }

    private static string ResolveConfigurationRoot(string? sharedSettingsPath, string? localSettingsPath, string fallbackDirectory)
    {
        if (!string.IsNullOrWhiteSpace(localSettingsPath))
        {
            return Path.GetDirectoryName(localSettingsPath)!;
        }

        if (!string.IsNullOrWhiteSpace(sharedSettingsPath))
        {
            return Path.GetDirectoryName(sharedSettingsPath)!;
        }

        return fallbackDirectory;
    }

    private static string? FindProjectDirectory(string runtimeDirectory)
    {
        DirectoryInfo? currentDirectory = new(runtimeDirectory);

        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, AppProjectFileName)))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return null;
    }

    private static string NormalizeDirectory(string path)
    {
        return Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private static SettingsSection? LoadSettingsSection(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty(SectionName, out var optionsElement))
        {
            if (!document.RootElement.TryGetProperty(LegacySectionName, out optionsElement))
            {
                return null;
            }

            return new SettingsSection(path, optionsElement.Clone(), LegacySectionName);
        }

        return new SettingsSection(path, optionsElement.Clone(), SectionName);
    }

    private static string ReadString(JsonElement? local, JsonElement? shared, string propertyName, string fallback)
    {
        var value = ReadOptionalString(local, shared, propertyName);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string? ReadOptionalString(JsonElement? local, JsonElement? shared, string propertyName)
    {
        if (TryReadString(local, propertyName, out var localValue))
        {
            return localValue;
        }

        return TryReadString(shared, propertyName, out var sharedValue)
            ? sharedValue
            : null;
    }

    private static bool TryReadString(JsonElement? source, string propertyName, out string? value)
    {
        value = null;
        return source is not null &&
               source.Value.TryGetProperty(propertyName, out var property) &&
               !string.IsNullOrWhiteSpace(property.GetString()) &&
               (value = property.GetString()) is not null;
    }

    private static string BuildConfigurationSourceSummary(SettingsSection? sharedSettingsSource, SettingsSection? localSettingsSource)
    {
        var sharedSummary = sharedSettingsSource is null
            ? $"{SharedSettingsFileName}: missing"
            : $"{sharedSettingsSource.Path} [{DescribeSection(sharedSettingsSource.SectionName)}]";
        var localSummary = localSettingsSource is null
            ? $"{LocalSettingsFileName}: not loaded"
            : $"{localSettingsSource.Path} [{DescribeSection(localSettingsSource.SectionName)}]";

        return $"{sharedSummary}; {localSummary}";
    }

    private static string DescribeSection(string sectionName)
    {
        return string.Equals(sectionName, LegacySectionName, StringComparison.Ordinal)
            ? "TylinkApi compatible"
            : SectionName;
    }

    private sealed record SettingsSection(string Path, JsonElement Section, string SectionName);
}
