using System.Security.Cryptography;
using System.Text;
using TylinkInspection.Core.Utilities;

namespace TylinkInspection.Infrastructure.OpenPlatform;

internal static class RsaCipher
{
    public static string DecryptWithPrivateKey(string encryptedData, string privateKey)
    {
        using var rsa = RSA.Create();
        ImportPrivateKey(rsa, privateKey);

        var cipherBytes = DecodeCipher(encryptedData);
        var blockSize = rsa.KeySize / 8;
        using var output = new MemoryStream();

        for (var offset = 0; offset < cipherBytes.Length; offset += blockSize)
        {
            var length = Math.Min(blockSize, cipherBytes.Length - offset);
            var block = cipherBytes.AsSpan(offset, length).ToArray();
            var decryptedBlock = rsa.Decrypt(block, RSAEncryptionPadding.Pkcs1);
            output.Write(decryptedBlock, 0, decryptedBlock.Length);
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static void ImportPrivateKey(RSA rsa, string privateKey)
    {
        var normalizedKey = NormalizeKey(privateKey);
        var keyBytes = Convert.FromBase64String(normalizedKey);

        try
        {
            rsa.ImportPkcs8PrivateKey(keyBytes, out _);
        }
        catch (CryptographicException)
        {
            rsa.ImportRSAPrivateKey(keyBytes, out _);
        }
    }

    private static string NormalizeKey(string privateKey)
    {
        return privateKey
            .Replace("-----BEGIN PRIVATE KEY-----", string.Empty, StringComparison.Ordinal)
            .Replace("-----END PRIVATE KEY-----", string.Empty, StringComparison.Ordinal)
            .Replace("-----BEGIN RSA PRIVATE KEY-----", string.Empty, StringComparison.Ordinal)
            .Replace("-----END RSA PRIVATE KEY-----", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static byte[] DecodeCipher(string encryptedData)
    {
        var normalized = encryptedData.Trim();

        if (TryDecodeHex(normalized, out var hexBytes))
        {
            return hexBytes;
        }

        return Convert.FromBase64String(normalized);
    }

    private static bool TryDecodeHex(string value, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if ((value.Length & 1) == 1)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (!Uri.IsHexDigit(value[i]))
            {
                return false;
            }
        }

        bytes = Convert.FromHexString(value);
        return true;
    }
}
