using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface ITokenCacheRepository
{
    OpenPlatformTokenCache? Load(string appId, string enterpriseUser);

    void Save(OpenPlatformTokenCache tokenCache);
}
