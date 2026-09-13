namespace Memory.Application.Agent;

using System.Text.Json;
using Memory.Application.Tools;

internal static class WebSearchFallback
{
    public const string ToolName = WebSearchAgentTool.ToolName;
    public const string CallId = "fallback_web_search";

    private static readonly string[] QueryPrefixes =
    [
        "najdi mi informace o ",
        "najdi informace o ",
        "najdi mi ",
        "najdi ",
        "vyhledej mi ",
        "vyhledej ",
        "hledej ",
        "find information about ",
        "find me information about ",
        "look up ",
        "search for ",
        "kdo je ",
        "who is ",
        "who was "
    ];

    private static readonly string[] LookupMarkers =
    [
        "najdi",
        "vyhledej",
        "hledej",
        "look up",
        "search for",
        "informace o",
        "kdo je",
        "who is",
        "who was",
        "find information"
    ];

    public static bool TryCreateCall(
        IReadOnlyList<ToolDefinition> advertised,
        string userMessage,
        out ToolCall call)
    {
        call = null!;
        if (!advertised.Any(tool => string.Equals(tool.Name, ToolName, StringComparison.Ordinal)))
        {
            return false;
        }

        if (!LooksLikeLookup(userMessage))
        {
            return false;
        }

        var query = ExtractQuery(userMessage);
        if (query.Length == 0)
        {
            return false;
        }

        call = new ToolCall(
            CallId,
            ToolName,
            JsonSerializer.Serialize(new Dictionary<string, string> { ["query"] = query }));
        return true;
    }

    internal static bool LooksLikeLookup(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var text = message.Trim();
        return LookupMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    internal static string ExtractQuery(string message)
    {
        var trimmed = message.Trim();
        foreach (var prefix in QueryPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[prefix.Length..];
                break;
            }
        }

        return trimmed.Trim().TrimEnd('?', '.', '!', ':');
    }
}
