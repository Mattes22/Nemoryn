namespace Memory.Application.Configuration;

internal static class MemoryAiBaseUrl
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("AI base URL is required.");
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("AI base URL must be an absolute HTTP(S) URL.");
        }

        if (uri.Scheme is not "http" and not "https")
        {
            throw new ArgumentException("AI base URL must be HTTP or HTTPS.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("AI base URL must not contain user info.");
        }

        var normalized = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return string.IsNullOrEmpty(normalized) ? uri.GetLeftPart(UriPartial.Authority) : normalized;
    }
}
