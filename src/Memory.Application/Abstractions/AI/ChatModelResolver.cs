namespace Memory.Application.Abstractions.AI;

using Microsoft.Extensions.Options;
using Memory.Application.Configuration;

internal sealed class ChatModelResolver(
    IAiModelCatalog catalog,
    IOptionsMonitor<MemoryAiOptions> memoryAiOptions) : IChatModelResolver
{
    public async Task<IReadOnlyList<string>> ListChatModelsAsync(CancellationToken cancellationToken = default)
    {
        var options = memoryAiOptions.CurrentValue;
        var listed = await catalog.ListAsync(cancellationToken);
        var names = new List<string>();
        foreach (var name in listed)
        {
            if (string.IsNullOrWhiteSpace(name) || AiModelName.Equals(name, options.EmbeddingModel))
            {
                continue;
            }

            if (!names.Any(existing => AiModelName.Equals(existing, name)))
            {
                names.Add(name.Trim());
            }
        }

        if (!string.IsNullOrWhiteSpace(options.ChatModel))
        {
            var selected = names.FirstOrDefault(existing => AiModelName.Equals(existing, options.ChatModel))
                ?? options.ChatModel.Trim();
            names.RemoveAll(existing => AiModelName.Equals(existing, selected));
            names.Insert(0, selected);
        }

        return names;
    }

    public async Task<string> ResolveChatModelAsync(
        string? requested,
        CancellationToken cancellationToken = default)
    {
        var options = memoryAiOptions.CurrentValue;
        var fallback = AiModelName.Require(options.ChatModel, "Chat model is not configured.");
        var name = requested?.Trim();
        if (string.IsNullOrEmpty(name) || AiModelName.Equals(name, fallback))
        {
            return fallback;
        }

        if (AiModelName.Equals(name, options.EmbeddingModel))
        {
            throw new ArgumentException($"Model '{name}' is the embedding model and cannot be used for chat.");
        }

        var listed = await ListChatModelsAsync(cancellationToken);
        var match = listed.FirstOrDefault(item => AiModelName.Equals(item, name));
        if (match is null)
        {
            throw new ArgumentException($"Unknown chat model '{name}'.");
        }

        return match;
    }
}
