using System.Text.Json;
using TylinkInspection.Core.Configuration;
using TylinkInspection.Core.Contracts;

namespace TylinkInspection.Infrastructure.Configuration;

public sealed class JsonMapProviderOptionsProvider : IMapProviderOptionsProvider
{
    private const string SectionName = "MapProvider";
    private const string SharedSettingsFileName = "appsettings.json";
    private const string LocalSettingsFileName = "appsettings.Local.json";
    private const string LowercaseLocalSettingsFileName = "appsettings.local.json";
    private const string AppProjectFileName = "TylinkInspection.App.csproj";

    private readonly string _runtimeDirectory;
    private readonly string? _preferredSourceDirectory;

    public JsonMapProviderOptionsProvider(string? runtimeDirectory = null, string? preferredSourceDirectory = null)
    {
        _runtimeDirectory = NormalizeDirectory(runtimeDirectory ?? AppContext.BaseDirectory);
        _preferredSourceDirectory = string.IsNullOrWhiteSpace(preferredSourceDirectory)
            ? FindProjectDirectory(_runtimeDirectory)
            : NormalizeDirectory(preferredSourceDirectory);
    }

    public AmapMapOptions GetOptions()
    {
        var sharedSettingsSource = LoadSettingsSection(EnumerateSharedSettingsPaths());
        var localSettingsSource = LoadSettingsSection(EnumerateLocalSettingsPaths());

        return new AmapMapOptions
        {
            JsApiKey = ReadString(localSettingsSource?.Section, sharedSettingsSource?.Section, "AmapWebJsApiKey", "your-amap-js-api-key"),
            SecurityJsCode = ReadString(localSettingsSource?.Section, sharedSettingsSource?.Section, "AmapSecurityJsCode", "your-amap-security-js-code"),
            JsApiVersion = ReadString(localSettingsSource?.Section, sharedSettingsSource?.Section, "AmapJsApiVersion", "2.0"),
            CoordinateSystem = ReadString(localSettingsSource?.Section, sharedSettingsSource?.Section, "CoordinateSystem", "GCJ-02")
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
            return null;
        }

        return new SettingsSection(path, optionsElement.Clone());
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

    private sealed record SettingsSection(string Path, JsonElement Section);
}
