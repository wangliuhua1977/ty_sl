using System.Text.Json;
using TylinkInspection.Core.Configuration;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Infrastructure.OpenPlatform;

public sealed class OpenPlatformClient : IOpenPlatformClient
{
    private readonly OpenPlatformRequestBuilder _requestBuilder;
    private readonly OpenPlatformFormSender _formSender;
    private readonly IOpenPlatformResponseDecryptor _responseDecryptor;

    public OpenPlatformClient(
        OpenPlatformRequestBuilder requestBuilder,
        OpenPlatformFormSender formSender,
        IOpenPlatformResponseDecryptor responseDecryptor)
    {
        _requestBuilder = requestBuilder;
        _formSender = formSender;
        _responseDecryptor = responseDecryptor;
    }

    public OpenPlatformResponseEnvelope<T> Execute<T>(string endpointPath, IReadOnlyDictionary<string, string> privateParameters, TylinkOpenPlatformOptions options)
    {
        var request = _requestBuilder.Build(endpointPath, privateParameters, options);
        var url = $"{options.BaseUrl.TrimEnd('/')}/{endpointPath.TrimStart('/')}";
        var rawResponse = _formSender.Send(url, request.PublicParameters, options.ApiVersion);

        using var document = JsonDocument.Parse(rawResponse);
        var root = document.RootElement;
        var code = root.TryGetProperty("code", out var codeElement) ? codeElement.GetInt32() : -1;
        var message = root.TryGetProperty("msg", out var msgElement) ? msgElement.GetString() ?? string.Empty : string.Empty;

        if (code != 0)
        {
            throw new OpenPlatformException(
                string.IsNullOrWhiteSpace(message) ? "平台返回失败响应。" : message,
                code.ToString());
        }

        if (!root.TryGetProperty("data", out var dataElement))
        {
            return new OpenPlatformResponseEnvelope<T> { Code = code, Message = message };
        }

        if (dataElement.ValueKind == JsonValueKind.String)
        {
            var decrypted = _responseDecryptor.Decrypt(dataElement.GetString() ?? string.Empty, options.AppSecret, options.RsaPrivateKey, options.Version);
            var data = JsonSerializer.Deserialize<T>(decrypted, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return new OpenPlatformResponseEnvelope<T> { Code = code, Message = message, Data = data };
        }

        return new OpenPlatformResponseEnvelope<T>
        {
            Code = code,
            Message = message,
            Data = JsonSerializer.Deserialize<T>(dataElement.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
    }
}
