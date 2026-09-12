namespace Memory.Application.Memories;

using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.AI;
using Memory.Application.Abstractions.Persistence;
using Memory.Application.Configuration;
using Memory.Application.Exceptions;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

internal sealed class MemoryService(
    IMemoryStore memoryStore,
    IEmbeddingProvider embeddingProvider,
    IOptions<MemoryAiOptions> memoryAiOptions) : IMemoryService
{
    public async Task<MemoryResponse> CreateAsync(
        CreateMemoryRequest request,
        CancellationToken cancellationToken = default)
    {
        EnumGuard.EnsureDefined(request.Type, nameof(request.Type));
        if (request.Scope is not null)
        {
            EnumGuard.EnsureDefined(request.Scope.Value, nameof(request.Scope));
        }

        var conversation = await memoryStore.GetConversationAsync(request.ConversationId, cancellationToken)
            ?? throw new NotFoundException("Conversation was not found.");

        if (request.SourceMessageId is not null)
        {
            var sourceMessage = await memoryStore.GetMessageAsync(request.SourceMessageId.Value, cancellationToken)
                ?? throw new NotFoundException("Source message was not found.");

            if (sourceMessage.ConversationId != request.ConversationId)
            {
                throw new ArgumentException(
                    "Source message does not belong to the conversation.",
                    nameof(request));
            }
        }

        var scope = request.Scope ?? MemoryScopeRules.DefaultFor(request.Type);
        var memory = new MemoryEntity(
            conversation.OwnerId,
            request.ConversationId,
            scope,
            request.Content,
            request.Type,
            request.Importance,
            request.Confidence,
            request.SourceMessageId,
            request.SourceSummary,
            request.ValidFrom,
            request.ValidUntil,
            MemoryOrigin.Explicit,
            request.Pin);

        memory.SetSourceMetadataJson(request.SourceMetadataJson);

        var duplicate = await memoryStore.GetActiveMemoryByFingerprintAsync(memory.Fingerprint, cancellationToken);
        if (duplicate is not null)
        {
            throw new ConflictException("An active memory with the same content already exists.");
        }

        await TrySetEmbeddingAsync(memory, cancellationToken);

        memoryStore.AddMemory(memory);
        memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForMemory(
            MemoryAuditAction.Created,
            memory,
            MemoryAuditActorKind.User,
            "Explicit memory created."));
        if (request.Pin)
        {
            memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForMemory(
                MemoryAuditAction.Pinned,
                memory,
                MemoryAuditActorKind.User,
                "Memory pinned on create."));
        }

        await memoryStore.SaveChangesAsync(cancellationToken);

        return ToResponse(memory);
    }

    public async Task<IReadOnlyList<MemoryResponse>> GetActiveForConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await memoryStore.GetConversationAsync(conversationId, cancellationToken)
            ?? throw new NotFoundException("Conversation was not found.");

        var memories = await memoryStore.GetActiveMemoriesAsync(
            MemoryLookup.ForConversation(conversation.OwnerId, conversation.Id),
            cancellationToken);

        return memories.Select(ToResponse).ToArray();
    }

    public async Task<IReadOnlyList<MemoryResponse>> GetActiveForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(ownerId));
        }

        var memories = await memoryStore.GetActiveMemoriesAsync(
            MemoryLookup.ForUser(ownerId.Trim()),
            cancellationToken);

        return memories.Select(ToResponse).ToArray();
    }

    public async Task<IReadOnlyList<MemorySearchMatch>> SearchAsync(
        Guid conversationId,
        SearchMemoriesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new ArgumentException("Search query is required.", nameof(request));
        }

        var ranked = await RankRelevantAsync(conversationId, request, cancellationToken);

        return ranked
            .Select(item => new MemorySearchMatch(item.Memory, item.Score, item.Similarity))
            .ToArray();
    }

    public async Task<IReadOnlyList<RankedMemory>> RetrieveAsync(
        Guid conversationId,
        SearchMemoriesRequest request,
        CancellationToken cancellationToken = default)
    {
        var stable = await RetrieveStableAsync(conversationId, request, cancellationToken);
        return stable.All;
    }

    public async Task<StableMemorySet> RetrieveStableAsync(
        Guid conversationId,
        SearchMemoriesRequest request,
        CancellationToken cancellationToken = default)
    {
        var conversation = await memoryStore.GetConversationAsync(conversationId, cancellationToken)
            ?? throw new NotFoundException("Conversation was not found.");

        var options = memoryAiOptions.Value;
        var now = DateTimeOffset.UtcNow;
        var lookup = MemoryLookup.ForRetrieval(conversation.OwnerId, conversation.Id);
        var active = await memoryStore.GetActiveMemoriesAsync(lookup, cancellationToken);
        var coreLimit = options.CoreMemoryLimit <= 0 ? 12 : options.CoreMemoryLimit;

        var core = active
            .Where(memory => MemoryStability.IsCore(memory, options.CoreImportanceThreshold))
            .OrderByDescending(memory => memory.IsPinned)
            .ThenByDescending(memory => memory.Importance)
            .ThenByDescending(memory => memory.Confidence)
            .Take(coreLimit)
            .Select(memory => new RankedMemory(
                ToResponse(memory),
                MemoryRanker.Score(memory, similarity: null, now),
                null))
            .ToArray();

        var relevant = await RankRelevantAsync(conversationId, request, cancellationToken);
        var coreIds = core.Select(item => item.Memory.Id).ToHashSet();
        var extras = relevant
            .Where(item => !coreIds.Contains(item.Memory.Id))
            .Take(NormalizeMaxRelevant(options))
            .ToArray();

        return new StableMemorySet(core, extras);
    }

    public async Task<MemoryResponse> PinAsync(Guid memoryId, CancellationToken cancellationToken = default)
    {
        var memory = await GetRequiredMemoryAsync(memoryId, cancellationToken);
        memory.Pin();
        memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForMemory(
            MemoryAuditAction.Pinned,
            memory,
            MemoryAuditActorKind.User,
            "Memory pinned."));
        await memoryStore.SaveChangesAsync(cancellationToken);
        return ToResponse(memory);
    }

    public async Task<MemoryResponse> UnpinAsync(Guid memoryId, CancellationToken cancellationToken = default)
    {
        var memory = await GetRequiredMemoryAsync(memoryId, cancellationToken);
        memory.Unpin();
        memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForMemory(
            MemoryAuditAction.Unpinned,
            memory,
            MemoryAuditActorKind.User,
            "Memory unpinned."));
        await memoryStore.SaveChangesAsync(cancellationToken);
        return ToResponse(memory);
    }

    public async Task<MemoryResponse> ForgetAsync(Guid memoryId, CancellationToken cancellationToken = default)
    {
        var memory = await GetRequiredMemoryAsync(memoryId, cancellationToken);
        memory.Forget();
        memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForMemory(
            MemoryAuditAction.Forgotten,
            memory,
            MemoryAuditActorKind.User,
            "Memory forgotten."));
        await memoryStore.SaveChangesAsync(cancellationToken);
        return ToResponse(memory);
    }

    public async Task<MemoryResponse> CorrectAsync(
        Guid memoryId,
        CorrectMemoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var memory = await GetRequiredMemoryAsync(memoryId, cancellationToken);
        var previous = memory.Content;
        var wasPinned = memory.IsPinned;
        memory.Correct(request.Content);

        if (request.Pin is true)
        {
            memory.Pin();
        }

        var duplicate = await memoryStore.GetActiveMemoryByFingerprintAsync(memory.Fingerprint, cancellationToken);
        if (duplicate is not null && duplicate.Id != memory.Id)
        {
            throw new ConflictException("An active memory with the same content already exists.");
        }

        await TrySetEmbeddingAsync(memory, cancellationToken);
        memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForMemory(
            MemoryAuditAction.Edited,
            memory,
            MemoryAuditActorKind.User,
            "Memory corrected.",
            previous));
        if (request.Pin is true && !wasPinned)
        {
            memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForMemory(
                MemoryAuditAction.Pinned,
                memory,
                MemoryAuditActorKind.User,
                "Memory pinned during correction."));
        }

        await memoryStore.SaveChangesAsync(cancellationToken);
        return ToResponse(memory);
    }

    private async Task<IReadOnlyList<RankedMemory>> RankRelevantAsync(
        Guid conversationId,
        SearchMemoriesRequest request,
        CancellationToken cancellationToken)
    {
        var conversation = await memoryStore.GetConversationAsync(conversationId, cancellationToken)
            ?? throw new NotFoundException("Conversation was not found.");

        var limit = NormalizeLimit(request.Limit);
        var options = memoryAiOptions.Value;
        var lookup = MemoryLookup.ForRetrieval(conversation.OwnerId, conversation.Id);
        var now = DateTimeOffset.UtcNow;
        var hasQuery = !string.IsNullOrWhiteSpace(request.Query);

        if (!hasQuery || !embeddingProvider.IsAvailable)
        {
            var active = await memoryStore.GetActiveMemoriesAsync(lookup, cancellationToken);

            return active
                .Select(memory => new RankedMemory(
                    ToResponse(memory),
                    MemoryRanker.Score(memory, similarity: null, now),
                    null))
                .Where(item => PassesRelevantThresholds(item, hasQuery: false, options))
                .OrderByDescending(item => item.Score)
                .Take(limit)
                .ToArray();
        }

        var queryEmbedding = await embeddingProvider.EmbedAsync(request.Query.Trim(), cancellationToken);
        var overFetch = Math.Max(limit * 4, 20);
        var matches = await memoryStore.SearchActiveMemoriesAsync(
            lookup,
            queryEmbedding,
            overFetch,
            cancellationToken);

        return matches
            .Select(match =>
            {
                var similarity = MemoryRanker.ToSimilarity(match.Distance);
                return new RankedMemory(
                    ToResponse(match.Memory),
                    MemoryRanker.Score(match.Memory, similarity, now),
                    similarity);
            })
            .Where(item => PassesRelevantThresholds(item, hasQuery: true, options))
            .OrderByDescending(item => item.Score)
            .Take(limit)
            .ToArray();
    }

    private async Task<MemoryEntity> GetRequiredMemoryAsync(Guid memoryId, CancellationToken cancellationToken)
    {
        return await memoryStore.GetMemoryAsync(memoryId, cancellationToken)
            ?? throw new NotFoundException("Memory was not found.");
    }

    private async Task TrySetEmbeddingAsync(MemoryEntity memory, CancellationToken cancellationToken)
    {
        if (!embeddingProvider.IsAvailable)
        {
            return;
        }

        var embedding = await embeddingProvider.EmbedAsync(memory.Content, cancellationToken);
        EnsureEmbeddingDimensions(embedding);
        memory.SetEmbedding(embedding);
    }

    private void EnsureEmbeddingDimensions(IReadOnlyList<float> embedding)
    {
        var expected = memoryAiOptions.Value.EmbeddingDimensions;

        if (embedding.Count != expected)
        {
            throw new InvalidOperationException(
                $"Embedding dimension mismatch. Expected {expected}, received {embedding.Count}.");
        }
    }

    private int NormalizeLimit(int? limit)
    {
        var configured = memoryAiOptions.Value.SearchLimit;
        if (configured <= 0)
        {
            configured = 10;
        }

        if (limit is null || limit <= 0)
        {
            return configured;
        }

        return Math.Min(limit.Value, Math.Max(configured, 50));
    }

    private static int NormalizeMaxRelevant(MemoryAiOptions options)
    {
        return options.MaxRelevantMemories <= 0 ? 8 : Math.Min(options.MaxRelevantMemories, 40);
    }

    private static bool PassesRelevantThresholds(RankedMemory item, bool hasQuery, MemoryAiOptions options)
    {
        if (item.Memory.IsPinned)
        {
            return true;
        }

        if (item.Score < options.MinRelevantScore)
        {
            return false;
        }

        return !hasQuery
            || item.Similarity is not null && item.Similarity >= options.MinRelevantSimilarity;
    }

    internal static MemoryResponse ToResponse(MemoryEntity memory)
    {
        return new MemoryResponse(
            memory.Id,
            memory.OwnerId,
            memory.ConversationId,
            memory.Scope,
            memory.SourceMessageId,
            memory.Content,
            memory.Type,
            memory.Status,
            memory.Origin,
            memory.IsPinned,
            memory.Importance,
            memory.Confidence,
            memory.ValidFrom,
            memory.ValidUntil,
            memory.SourceSummary,
            memory.SourceMetadataJson,
            memory.CreatedAt,
            memory.UpdatedAt);
    }
}
