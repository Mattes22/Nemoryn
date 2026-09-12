namespace Memory.Application.OpenAiCompatible;

public static class OpenAiCompatibleSideTask
{
    public static string? Detect(string? metadataTask, string? userMessage)
    {
        if (!string.IsNullOrWhiteSpace(metadataTask))
        {
            return metadataTask.Trim();
        }

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return null;
        }

        var text = userMessage;
        var looksLikeOpenWebUiTask = Contains(text, "### Task:") || Contains(text, "<chat_history>");
        if (!looksLikeOpenWebUiTask)
        {
            return null;
        }

        if (Contains(text, "3-5 word title") || Contains(text, "\"title\": \"your concise title here\""))
        {
            return "title_generation";
        }

        if (Contains(text, "1-3 broad tags") || Contains(text, "\"tags\": [\"tag1\""))
        {
            return "tags_generation";
        }

        if (Contains(text, "follow-up") || Contains(text, "follow up"))
        {
            return "follow_up_generation";
        }

        if (Contains(text, "image generation"))
        {
            return "image_prompt_generation";
        }

        return "open_webui_task";
    }

    private static bool Contains(string text, string value)
    {
        return text.Contains(value, StringComparison.OrdinalIgnoreCase);
    }
}
