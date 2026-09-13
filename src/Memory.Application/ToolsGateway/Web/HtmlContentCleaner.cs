namespace Memory.Application.ToolsGateway.Web;

using System.Net;
using System.Text;
using System.Text.RegularExpressions;

internal static partial class HtmlContentCleaner
{
    private const int TitleMaxChars = 300;

    public static WebPageContent Clean(
        string url,
        int statusCode,
        string? contentType,
        byte[] body,
        int maxTextChars)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentNullException.ThrowIfNull(body);

        var mediaType = MediaType(contentType);
        if (IsBinary(mediaType))
        {
            return new WebPageContent(url, Title: null, string.Empty, statusCode, contentType);
        }

        var raw = Encoding.UTF8.GetString(body);
        if (!LooksLikeHtml(mediaType, raw))
        {
            return new WebPageContent(url, Title: null, Truncate(NormalizePlain(raw), maxTextChars), statusCode, contentType);
        }

        var title = ExtractTitle(raw);
        var withoutNoise = StripNoise(raw);
        var withBreaks = BlockBreaks().Replace(withoutNoise, "\n");
        var withoutTags = Tags().Replace(withBreaks, " ");
        var text = Truncate(NormalizePlain(WebUtility.HtmlDecode(withoutTags)), maxTextChars);

        return new WebPageContent(url, title, text, statusCode, contentType);
    }

    private static string? ExtractTitle(string html)
    {
        var match = TitleTag().Match(html);
        if (!match.Success)
        {
            return null;
        }

        var title = NormalizePlain(WebUtility.HtmlDecode(Tags().Replace(match.Groups[1].Value, " ")));
        if (title.Length == 0)
        {
            return null;
        }

        return title.Length <= TitleMaxChars ? title : title[..TitleMaxChars];
    }

    private static string StripNoise(string html)
    {
        var withoutScripts = ScriptBlocks().Replace(html, " ");
        var withoutStyles = StyleBlocks().Replace(withoutScripts, " ");
        return NoiseBlocks().Replace(withoutStyles, " ");
    }

    private static string NormalizePlain(string text)
    {
        var decoded = text.Replace('\u00a0', ' ');
        return Whitespace().Replace(decoded, " ").Trim();
    }

    private static string Truncate(string text, int maxChars)
    {
        if (maxChars <= 0 || text.Length <= maxChars)
        {
            return text;
        }

        return text[..maxChars];
    }

    private static string? MediaType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var separator = contentType.IndexOf(';');
        var media = separator < 0 ? contentType : contentType[..separator];
        return media.Trim().ToLowerInvariant();
    }

    private static bool LooksLikeHtml(string? mediaType, string raw)
    {
        if (mediaType is "text/html" or "application/xhtml+xml")
        {
            return true;
        }

        if (mediaType is not null && !mediaType.StartsWith("text/", StringComparison.Ordinal))
        {
            return false;
        }

        return raw.Contains("<html", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("<body", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("<title", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBinary(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return false;
        }

        return mediaType.StartsWith("image/", StringComparison.Ordinal)
            || mediaType.StartsWith("audio/", StringComparison.Ordinal)
            || mediaType.StartsWith("video/", StringComparison.Ordinal)
            || mediaType.StartsWith("font/", StringComparison.Ordinal)
            || mediaType is "application/octet-stream"
            || mediaType is "application/pdf"
            || mediaType is "application/zip";
    }

    [GeneratedRegex(@"<(script|style|noscript|svg|iframe|object|embed)\b[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex NoiseBlocks();

    [GeneratedRegex(@"<script\b[^>]*>.*?</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptBlocks();

    [GeneratedRegex(@"<style\b[^>]*>.*?</style>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StyleBlocks();

    [GeneratedRegex(@"<(?:br|p|div|tr|li|h[1-6]|blockquote|section|article|header|footer|nav)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockBreaks();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"<title\b[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleTag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
