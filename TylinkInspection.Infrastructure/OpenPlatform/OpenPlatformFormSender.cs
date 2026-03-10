using System.Net.Http.Headers;
using System.Text;

namespace TylinkInspection.Infrastructure.OpenPlatform;

public sealed class OpenPlatformFormSender
{
    private readonly HttpClient _httpClient;

    public OpenPlatformFormSender(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public string Send(string requestUrl, IReadOnlyDictionary<string, string> publicParameters, string apiVersion)
    {
        if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out var requestUri) || requestUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new OpenPlatformException("开放平台请求地址必须为 HTTPS。", "invalid_url");
        }

        if (string.IsNullOrWhiteSpace(apiVersion) || apiVersion != "2.0")
        {
            throw new OpenPlatformException("开放平台请求头 apiVersion 必须为 2.0。", "invalid_api_version");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = new FormUrlEncodedContent(publicParameters)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("apiVersion", apiVersion);
        request.Content!.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded")
        {
            CharSet = Encoding.UTF8.WebName
        };

        using var response = _httpClient.Send(request);
        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            throw new OpenPlatformException($"平台请求失败，HTTP {(int)response.StatusCode}", response.StatusCode.ToString());
        }

        return body;
    }
}
