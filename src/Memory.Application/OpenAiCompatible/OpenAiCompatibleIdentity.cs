namespace Memory.Application.OpenAiCompatible;

public static class OpenAiCompatibleIdentity
{
    public static string ResolveOwnerId(params string?[] candidates)
    {
        return FirstNonEmpty(candidates) ?? "anonymous";
    }

    public static string ResolveConversationKey(string ownerId, params string?[] candidates)
    {
        return FirstNonEmpty(candidates) ?? $"{ownerId.Trim()}-default";
    }

    private static string? FirstNonEmpty(IEnumerable<string?> values)
    {
        return values
            .Select(value => value?.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
