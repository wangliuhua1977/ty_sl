using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface ITokenService
{
    OpenPlatformTokenResult GetAvailableToken();

    OpenPlatformTokenResult RefreshToken();

    OpenPlatformTokenState GetTokenState();
}
