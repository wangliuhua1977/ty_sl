namespace TylinkInspection.Infrastructure.OpenPlatform;

public interface IOpenPlatformResponseDecryptor
{
    string Decrypt(string encryptedData, string appSecret, string rsaPrivateKey, string version);
}
