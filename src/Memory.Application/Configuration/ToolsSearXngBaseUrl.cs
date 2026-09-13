namespace Memory.Application.Configuration;

internal static class ToolsSearXngBaseUrl
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (!TryCreate(value, out var uri))
        {
            throw new ArgumentException("SearXNG base URL must be an absolute HTTP(S) URL without credentials.");
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    public static bool TryCreate(string? value, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(parsed.Host)
            || !string.IsNullOrEmpty(parsed.UserInfo))
        {
            return false;
        }

        uri = new Uri(parsed.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/", UriKind.Absolute);
        return true;
    }
}
