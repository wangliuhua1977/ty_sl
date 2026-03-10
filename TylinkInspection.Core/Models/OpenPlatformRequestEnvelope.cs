namespace TylinkInspection.Core.Models;

public sealed class OpenPlatformRequestEnvelope
{
    public required string EndpointPath { get; init; }

    public required IReadOnlyDictionary<string, string> PrivateParameters { get; init; }

    public required IReadOnlyDictionary<string, string> PublicParameters { get; init; }
}
