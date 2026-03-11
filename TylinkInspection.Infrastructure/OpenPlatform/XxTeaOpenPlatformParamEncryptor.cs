using TylinkInspection.Core.Utilities;

namespace TylinkInspection.Infrastructure.OpenPlatform;

public sealed class XxTeaOpenPlatformParamEncryptor : IOpenPlatformParamEncryptor
{
    public string Encrypt(IReadOnlyDictionary<string, string> privateParameters, string appSecret, string version)
    {
        if (OpenPlatformVersionPolicy.IsInvalidPrefixedCurrentVersion(version))
        {
            throw new OpenPlatformException(OpenPlatformVersionPolicy.BuildCurrentVersionRequirementMessage(), "invalid_version");
        }

        if (!OpenPlatformVersionPolicy.IsCurrentVersion(version) &&
            !OpenPlatformVersionPolicy.IsLegacyCompatibleVersion(version))
        {
            throw new OpenPlatformException(
                $"Version={version} \u65e0\u6548\u3002{OpenPlatformVersionPolicy.BuildCurrentVersionRequirementMessage()}",
                "unsupported_version");
        }

        var raw = string.Join("&", privateParameters.Select(pair => $"{pair.Key}={pair.Value}"));
        return XxTeaCipher.Encrypt(raw, appSecret);
    }
}
