using TylinkInspection.Core.Configuration;
using TylinkInspection.Core.Models;
using TylinkInspection.Core.Utilities;
using TylinkInspection.Infrastructure.Configuration;

namespace TylinkInspection.Infrastructure.OpenPlatform;

public sealed class OpenPlatformRequestBuilder
{
    private readonly IOpenPlatformParamEncryptor _paramEncryptor;
    private readonly OpenPlatformSignatureGenerator _signatureGenerator;
    private readonly OpenPlatformOptionsValidator _optionsValidator;

    public OpenPlatformRequestBuilder(
        IOpenPlatformParamEncryptor paramEncryptor,
        OpenPlatformSignatureGenerator signatureGenerator,
        OpenPlatformOptionsValidator optionsValidator)
    {
        _paramEncryptor = paramEncryptor;
        _signatureGenerator = signatureGenerator;
        _optionsValidator = optionsValidator;
    }

    public OpenPlatformRequestEnvelope Build(string endpointPath, IReadOnlyDictionary<string, string> privateParameters, TylinkOpenPlatformOptions options)
    {
        var errors = _optionsValidator.Validate(options);
        if (errors.Count > 0)
        {
            throw new OpenPlatformException(string.Join(" ", errors), "invalid_options");
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var encryptedParams = _paramEncryptor.Encrypt(privateParameters, options.AppSecret, OpenPlatformVersionPolicy.CurrentVersion);

        var publicParameters = new Dictionary<string, string>
        {
            ["appId"] = options.AppId,
            ["clientType"] = options.ClientType,
            ["params"] = encryptedParams,
            ["timestamp"] = timestamp,
            ["version"] = OpenPlatformVersionPolicy.CurrentVersion,
            ["apiVersion"] = OpenPlatformVersionPolicy.ApiVersion
        };

        publicParameters["signature"] = _signatureGenerator.Generate(publicParameters, options.AppSecret);

        return new OpenPlatformRequestEnvelope
        {
            EndpointPath = endpointPath,
            PrivateParameters = new Dictionary<string, string>(privateParameters),
            PublicParameters = publicParameters
        };
    }
}
