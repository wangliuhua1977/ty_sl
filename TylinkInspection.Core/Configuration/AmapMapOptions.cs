namespace TylinkInspection.Core.Configuration;

public sealed class AmapMapOptions
{
    public string JsApiKey { get; init; } = "your-amap-js-api-key";

    public string SecurityJsCode { get; init; } = "your-amap-security-js-code";

    public string JsApiVersion { get; init; } = "2.0";

    public string CoordinateSystem { get; init; } = "GCJ-02";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(JsApiKey) &&
        !string.Equals(JsApiKey, "your-amap-js-api-key", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(SecurityJsCode) &&
        !string.Equals(SecurityJsCode, "your-amap-security-js-code", StringComparison.OrdinalIgnoreCase);
}
