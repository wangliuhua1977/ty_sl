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
