namespace TylinkInspection.Infrastructure.OpenPlatform;

public interface IOpenPlatformParamEncryptor
{
    string Encrypt(IReadOnlyDictionary<string, string> privateParameters, string appSecret, string version);
}
