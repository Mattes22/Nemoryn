namespace Memory.Infrastructure.AI;

using Memory.Application.Configuration;

internal static class OpenAiAvailability
{
    public static bool IsConfigured(MemoryAiOptions options)
    {
        return string.Equals(options.Provider, "OpenAI", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(options.ApiKey)
            && !string.IsNullOrWhiteSpace(options.BaseUrl);
    }
}
