using System.Security.Cryptography;
using System.Text;

namespace TylinkInspection.Infrastructure.OpenPlatform;

public sealed class OpenPlatformSignatureGenerator
{
    public string Generate(IReadOnlyDictionary<string, string> requestData, string appSecret)
    {
        var raw = new StringBuilder()
            .Append(ReadRequired(requestData, "appId"))
            .Append(ReadRequired(requestData, "clientType"))
            .Append(ReadRequired(requestData, "params"))
            .Append(ReadRequired(requestData, "timestamp"))
            .Append(ReadRequired(requestData, "version"))
            .ToString();

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string ReadRequired(IReadOnlyDictionary<string, string> requestData, string key)
    {
        if (!requestData.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new OpenPlatformException($"\u751f\u6210\u7b7e\u540d\u65f6\u7f3a\u5c11\u516c\u5171\u53c2\u6570 {key}\u3002", "missing_signature_parameter");
        }

        return value;
    }
}
