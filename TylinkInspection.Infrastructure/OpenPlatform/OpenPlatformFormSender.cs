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
            throw new OpenPlatformException("\u5f00\u653e\u5e73\u53f0\u8bf7\u6c42\u5730\u5740\u5fc5\u987b\u4e3a HTTPS\u3002", "invalid_url");
        }

        if (string.IsNullOrWhiteSpace(apiVersion) || apiVersion != "2.0")
        {
            throw new OpenPlatformException("\u5f00\u653e\u5e73\u53f0\u8bf7\u6c42\u5934 apiVersion \u5fc5\u987b\u4e3a 2.0\u3002", "invalid_api_version");
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
            throw new OpenPlatformException($"\u5e73\u53f0\u8bf7\u6c42\u5931\u8d25\uff0cHTTP {(int)response.StatusCode}", response.StatusCode.ToString());
        }

        return body;
    }
}
