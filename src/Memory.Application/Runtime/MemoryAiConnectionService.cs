namespace Memory.Application.Runtime;

using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.AI;
using Memory.Application.Configuration;

internal sealed class MemoryAiConnectionService(
    IOptionsMonitor<MemoryAiOptions> memoryAiOptions,
    IOptionsMonitorCache<MemoryAiOptions> cache,
    MemoryAiConnectionRuntime runtime,
    IChatModelResolver chatModelResolver,
    IMemoryAiConnectionStore store) : IMemoryAiConnectionService
{
    public async Task<MemoryAiConnectionResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var options = memoryAiOptions.CurrentValue;
        IReadOnlyList<string> models = [];
        string? modelsError = null;
        try
        {
            models = await chatModelResolver.ListChatModelsAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            modelsError = exception.GetBaseException().Message;
            models = string.IsNullOrWhiteSpace(options.ChatModel) ? [] : [options.ChatModel.Trim()];
        }

        return ToResponse(options, models, modelsError, store.Load());
    }

    public async Task<MemoryAiConnectionResponse> SetAsync(
        MemoryAiConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = new MemoryAiConnectionSettings(
            MemoryAiBaseUrl.Normalize(request.BaseUrl),
            AiModelName.Require(request.ChatModel, "Chat model is required."),
            AiModelName.Require(request.EmbeddingModel, "Embedding model is required."));

        runtime.BaseUrl = settings.BaseUrl;
        runtime.ChatModel = settings.ChatModel;
        runtime.EmbeddingModel = settings.EmbeddingModel;
        var persisted = store.TrySave(settings, out var persistError);
        cache.TryRemove(Options.DefaultName);

        var response = await GetAsync(cancellationToken);
        return response with
        {
            Persisted = persisted,
            PersistError = persistError
        };
    }

    private static MemoryAiConnectionResponse ToResponse(
        MemoryAiOptions options,
        IReadOnlyList<string> models,
        string? modelsError,
        MemoryAiConnectionSettings? stored)
    {
        var persisted = stored is not null
            && string.Equals(stored.BaseUrl, options.BaseUrl, StringComparison.Ordinal)
            && AiModelName.Equals(stored.ChatModel, options.ChatModel)
            && AiModelName.Equals(stored.EmbeddingModel, options.EmbeddingModel);

        return new MemoryAiConnectionResponse(
            options.Provider,
            options.BaseUrl,
            options.ChatModel,
            options.EmbeddingModel,
            models,
            modelsError,
            persisted,
            PersistError: null);
    }
}
