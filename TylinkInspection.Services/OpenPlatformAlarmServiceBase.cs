using System.Globalization;
using System.Text.Json;
using TylinkInspection.Core.Configuration;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;
using TylinkInspection.Infrastructure.OpenPlatform;

namespace TylinkInspection.Services;

public abstract class OpenPlatformAlarmServiceBase
{
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd HH:mm:ss:fff",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:sszzz",
        "yyyy-MM-ddTHH:mm:ss.fffzzz"
    ];

    private readonly IOpenPlatformOptionsProvider _optionsProvider;
    private readonly ITokenService _tokenService;
    private readonly IOpenPlatformClient _openPlatformClient;

    protected OpenPlatformAlarmServiceBase(
        IOpenPlatformOptionsProvider optionsProvider,
        ITokenService tokenService,
        IOpenPlatformClient openPlatformClient)
    {
        _optionsProvider = optionsProvider;
        _tokenService = tokenService;
        _openPlatformClient = openPlatformClient;
    }

    protected OpenPlatformResponseEnvelope<JsonElement> Execute(string endpointPath, IReadOnlyDictionary<string, string> privateParameters)
    {
        try
        {
            var options = _optionsProvider.GetOptions();
            return _openPlatformClient.Execute<JsonElement>(endpointPath, BuildAuthorizedParameters(privateParameters, options), options);
        }
        catch (OpenPlatformException ex)
        {
            throw Translate(ex);
        }
        catch (PlatformServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PlatformServiceException("\u5f00\u653e\u5e73\u53f0\u8c03\u7528\u5931\u8d25\u3002", PlatformErrorCategory.Unknown, null, ex);
        }
    }

    protected static JsonElement UnwrapResponseData(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return element;
        }

        if (element.TryGetProperty("data", out var dataElement))
        {
            return dataElement;
        }

        if (element.TryGetProperty("Data", out dataElement))
        {
            return dataElement;
        }

        return element;
    }

    protected static IReadOnlyList<JsonElement> ReadArray(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(propertyName, out var arrayElement) &&
                arrayElement.ValueKind == JsonValueKind.Array)
            {
                return arrayElement.EnumerateArray().ToList();
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().ToList();
        }

        return Array.Empty<JsonElement>();
    }

    protected static string ReadString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetPropertyIgnoreCase(element, propertyName, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            if (property.ValueKind == JsonValueKind.Number ||
                property.ValueKind == JsonValueKind.True ||
                property.ValueKind == JsonValueKind.False)
            {
                return property.ToString();
            }
        }

        return string.Empty;
    }

    protected static int? ReadInt32(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetPropertyIgnoreCase(element, propertyName, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var numberValue))
            {
                return numberValue;
            }

            if (property.ValueKind == JsonValueKind.String &&
                int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out numberValue))
            {
                return numberValue;
            }
        }

        return null;
    }

    protected static long? ReadInt64(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetPropertyIgnoreCase(element, propertyName, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var numberValue))
            {
                return numberValue;
            }

            if (property.ValueKind == JsonValueKind.String &&
                long.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out numberValue))
            {
                return numberValue;
            }
        }

        return null;
    }

    protected static DateTimeOffset? ReadDateTimeOffset(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = ReadString(element, propertyName);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (DateTimeOffset.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var exactValue))
            {
                return exactValue;
            }

            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedValue))
            {
                return parsedValue;
            }
        }

        return null;
    }

    protected static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    protected static string JoinCsv(IReadOnlyList<int> values)
    {
        return string.Join(",", values);
    }

    protected static string FormatOpenPlatformTime(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss:fff", CultureInfo.InvariantCulture);
    }

    protected static string NormalizeText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private IReadOnlyDictionary<string, string> BuildAuthorizedParameters(IReadOnlyDictionary<string, string> privateParameters, TylinkOpenPlatformOptions options)
    {
        var token = _tokenService.GetAvailableToken();
        var parameters = new Dictionary<string, string>(privateParameters, StringComparer.Ordinal)
        {
            ["accessToken"] = token.AccessToken
        };

        if (!string.IsNullOrWhiteSpace(options.EnterpriseUser))
        {
            parameters["enterpriseUser"] = options.EnterpriseUser;
        }

        if (!string.IsNullOrWhiteSpace(options.ParentUser))
        {
            parameters["parentUser"] = options.ParentUser!;
        }

        return parameters;
    }

    private static PlatformServiceException Translate(OpenPlatformException exception)
    {
        var category = Classify(exception);
        return new PlatformServiceException(exception.Message, category, exception.ErrorCode, exception);
    }

    private static PlatformErrorCategory Classify(OpenPlatformException exception)
    {
        var message = exception.Message ?? string.Empty;
        var errorCode = exception.ErrorCode ?? string.Empty;

        if (errorCode.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            errorCode is "401" or "403")
        {
            return PlatformErrorCategory.Token;
        }

        if (errorCode.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("\u53c2\u6570", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("\u914d\u7f6e", StringComparison.OrdinalIgnoreCase))
        {
            return PlatformErrorCategory.Parameter;
        }

        return PlatformErrorCategory.Platform;
    }
}
