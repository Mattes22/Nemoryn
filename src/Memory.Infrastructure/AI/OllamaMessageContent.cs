namespace Memory.Infrastructure.AI;

internal static class OllamaMessageContent
{
    public static string? ReadChat(OllamaChatMessage? message)
    {
        if (!string.IsNullOrWhiteSpace(message?.Content))
        {
            return message.Content.Trim();
        }

        if (!string.IsNullOrWhiteSpace(message?.Thinking))
        {
            return message.Thinking.Trim();
        }

        if (!string.IsNullOrWhiteSpace(message?.Reasoning))
        {
            return message.Reasoning.Trim();
        }

        return null;
    }

    public static string? ReadJson(OllamaChatMessage? message)
    {
        foreach (var part in new[] { message?.Content, message?.Thinking, message?.Reasoning })
        {
            var json = ExtractJsonObject(part);
            if (json is not null)
            {
                return json;
            }
        }

        return ReadChat(message);
    }

    private static string? ExtractJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = StripMarkdownFence(text.Trim());
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return trimmed[start..(end + 1)];
    }

    private static string StripMarkdownFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstLineEnd = text.IndexOf('\n');
        if (firstLineEnd < 0)
        {
            return text;
        }

        var body = text[(firstLineEnd + 1)..].Trim();
        if (body.EndsWith("```", StringComparison.Ordinal))
        {
            body = body[..^3].Trim();
        }

        return body;
    }
}
