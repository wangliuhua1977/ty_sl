namespace TylinkInspection.Core.Utilities;

public static class SensitiveDataMasker
{
    public static string MaskAppId(string? value)
    {
        return Mask(value, 3, 3);
    }

    public static string MaskEnterpriseUser(string? value)
    {
        return Mask(value, 3, 2);
    }

    public static string MaskToken(string? value)
    {
        return Mask(value, 4, 4);
    }

    public static string MaskWebhook(string? value)
    {
        return Mask(value, 8, 6);
    }

    public static string MaskUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "--";
        }

        var trimmed = value.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return Mask(trimmed, 8, 6);
        }

        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Fragment = string.Empty
        };

        if (string.IsNullOrWhiteSpace(uri.Query))
        {
            return builder.Uri.GetLeftPart(UriPartial.Path);
        }

        var maskedParameters = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(item =>
            {
                var parts = item.Split('=', 2);
                var key = parts[0];
                return string.IsNullOrWhiteSpace(key)
                    ? string.Empty
                    : $"{key}=***";
            })
            .Where(item => !string.IsNullOrWhiteSpace(item));

        builder.Query = string.Join("&", maskedParameters);
        return builder.Uri.ToString();
    }

    private static string Mask(string? value, int keepStart, int keepEnd)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "--";
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= keepStart + keepEnd)
        {
            return new string('*', trimmed.Length);
        }

        return $"{trimmed[..keepStart]}***{trimmed[^keepEnd..]}";
    }
}
