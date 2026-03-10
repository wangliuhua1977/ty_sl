using TylinkInspection.Core.Utilities;

namespace TylinkInspection.Infrastructure.OpenPlatform;

public sealed class PlaceholderOpenPlatformResponseDecryptor : IOpenPlatformResponseDecryptor
{
    public string Decrypt(string encryptedData, string appSecret, string rsaPrivateKey, string version)
    {
        if (OpenPlatformVersionPolicy.IsCurrentVersion(version))
        {
            return RsaCipher.DecryptWithPrivateKey(encryptedData, rsaPrivateKey);
        }

        if (OpenPlatformVersionPolicy.IsLegacyCompatibleVersion(version))
        {
            return XxTeaCipher.Decrypt(encryptedData, appSecret);
        }

        throw new OpenPlatformException(
            $"Version={version} 无效。{OpenPlatformVersionPolicy.BuildCurrentVersionRequirementMessage()}",
            "invalid_version");
    }
}
