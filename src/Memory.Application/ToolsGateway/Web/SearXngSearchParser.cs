namespace Memory.Application.ToolsGateway.Web;

using System.Globalization;
using System.Text.Json;

internal static class SearXngSearchParser
{
    public const string ProviderName = "SearXNG";

    public static WebSearchResult Parse(string json, string query, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("SearXNG response must be a JSON object.");
        }

        var hits = new List<WebSearchHit>();
        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in results.EnumerateArray())
            {
                if (hits.Count >= maxResults)
                {
                    break;
                }

                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var url = ReadString(item, "url");
                if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    continue;
                }

                hits.Add(new WebSearchHit(
                    Title: ReadString(item, "title") ?? uri.Host,
                    Url: uri.ToString(),
                    Snippet: ReadString(item, "content") ?? string.Empty,
                    Source: ReadString(item, "engine") ?? ReadFirstString(item, "engines") ?? ProviderName,
                    Score: ReadDouble(item, "score")));
            }
        }

        return new WebSearchResult(query, ProviderName, hits);
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : value.ToString().Trim();
    }

    private static string? ReadFirstString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var text = item.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static double? ReadDouble(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
