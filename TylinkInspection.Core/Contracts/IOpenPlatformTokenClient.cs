using TylinkInspection.Core.Configuration;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IOpenPlatformTokenClient
{
    OpenPlatformTokenResult RequestAccessToken(TylinkOpenPlatformOptions options);

    OpenPlatformTokenResult RefreshAccessToken(TylinkOpenPlatformOptions options, string refreshToken);
}
