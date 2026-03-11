namespace TylinkInspection.Core.Models;

public sealed class ReportCountSegment
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public int Count { get; init; }

    public double Ratio { get; init; }

    public string DetailText { get; init; } = string.Empty;
}

public sealed class ReportTrendPoint
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public int Value { get; init; }

    public DateTimeOffset BucketTime { get; init; }

    public string DetailText { get; init; } = string.Empty;
}
