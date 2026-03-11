namespace TylinkInspection.Core.Models;

public sealed class ThemePreference
{
    public required string ThemeKey { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
