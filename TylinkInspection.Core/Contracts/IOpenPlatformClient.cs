using TylinkInspection.Core.Configuration;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IOpenPlatformClient
{
    OpenPlatformResponseEnvelope<T> Execute<T>(
        string endpointPath,
        IReadOnlyDictionary<string, string> privateParameters,
        TylinkOpenPlatformOptions options);
}
