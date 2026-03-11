namespace TylinkInspection.Core.Models;

public sealed class FaultClosureQuery
{
    public string Status { get; init; } = string.Empty;

    public string FaultType { get; init; } = string.Empty;

    public string SourceType { get; init; } = string.Empty;

    public bool PendingRecheckOnly { get; init; }

    public bool FocusedOnly { get; init; }
}
