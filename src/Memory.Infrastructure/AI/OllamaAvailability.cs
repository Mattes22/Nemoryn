namespace Memory.Infrastructure.AI;

using Memory.Application.Configuration;

internal static class OllamaAvailability
{
    public static bool IsConfigured(MemoryAiOptions options)
    {
        return string.Equals(options.Provider, "Ollama", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(options.BaseUrl);
    }
}
