namespace Memory.Application.Memories;

using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.AI;
using Memory.Application.Abstractions.Persistence;
using Memory.Application.Configuration;
using Memory.Application.Context;
using Memory.Application.Exceptions;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

internal sealed class MemoryRecallEvaluationService(
    IMemoryStore memoryStore,
    IMemoryService memoryService,
    IEmbeddingProvider embeddingProvider,
    IOptions<MemoryAiOptions> memoryAiOptions) : IMemoryRecallEvaluationService
{
    public const int MaxCases = 20;

    public async Task<MemoryRecallEvaluationResponse> EvaluateAsync(
        Guid conversationId,
        EvaluateMemoryRecallRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cases = request.Cases ?? [];
        if (cases.Count == 0)
        {
            throw new ArgumentException("At least one recall case is required.", nameof(request));
        }

        if (cases.Count > MaxCases)
        {
            throw new ArgumentException($"At most {MaxCases} recall cases can be evaluated at once.", nameof(request));
        }

        var conversation = await memoryStore.GetConversationAsync(conversationId, cancellationToken)
            ?? throw new NotFoundException("Conversation was not found.");

        var options = memoryAiOptions.Value;
        var now = DateTimeOffset.UtcNow;
        var results = new List<MemoryRecallCaseResult>(cases.Count);

        foreach (var recallCase in cases)
        {
            results.Add(await EvaluateCaseAsync(
                conversation.OwnerId,
                conversation.Id,
                recallCase,
                request.Limit,
                options,
                now,
                cancellationToken));
        }

        var evaluable = results.Where(result => result.HasExpectation).ToArray();
        var hits = evaluable.Count(result => result.Hit);
        var summary = new MemoryRecallSummary(
            results.Count,
            evaluable.Length,
            hits,
            evaluable.Length - hits,
            evaluable.Length == 0 ? null : (double)hits / evaluable.Length,
            evaluable.Length == 0
                ? null
                : evaluable.Average(result => result.ReciprocalRank ?? 0d));

        return new MemoryRecallEvaluationResponse(
            conversation.Id,
            conversation.OwnerId,
            new MemoryRecallThresholds(
                MemoryPolicyPresets.Normalize(options.Policy),
                options.MinRelevantSimilarity,
                options.MinRelevantScore,
                options.MaxRelevantMemories <= 0 ? 8 : Math.Min(options.MaxRelevantMemories, 40),
                options.CoreMemoryLimit <= 0 ? 12 : options.CoreMemoryLimit,
                options.CoreImportanceThreshold,
                embeddingProvider.IsAvailable),
            summary,
            results);
    }

    private async Task<MemoryRecallCaseResult> EvaluateCaseAsync(
        string ownerId,
        Guid conversationId,
        MemoryRecallCase recallCase,
        int? limit,
        MemoryAiOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(recallCase.Query))
        {
            throw new ArgumentException("Recall query is required.", nameof(recallCase));
        }

        var query = recallCase.Query.Trim();
        var stable = await memoryService.RetrieveStableAsync(
            conversationId,
            new SearchMemoriesRequest(query, limit),
            cancellationToken);
        var selected = MapSelected(stable, now);
        var selectedById = selected.ToDictionary(item => item.Memory.Id);
        IReadOnlyList<float>? queryEmbedding = null;
        if (embeddingProvider.IsAvailable)
        {
            queryEmbedding = await embeddingProvider.EmbedAsync(query, cancellationToken);
        }

        var expectations = await ResolveExpectationsAsync(
            ownerId,
            conversationId,
            recallCase,
            selected,
            selectedById,
            queryEmbedding,
            options,
            now,
            cancellationToken);
        var hasExpectation = expectations.Count > 0;
        var foundRanks = expectations
            .Where(expectation => expectation.Found && expectation.Rank is not null)
            .Select(expectation => expectation.Rank!.Value)
            .ToArray();
        var hit = hasExpectation && expectations.All(expectation => expectation.Found);
        int? bestRank = foundRanks.Length == 0 ? null : foundRanks.Min();

        return new MemoryRecallCaseResult(
            string.IsNullOrWhiteSpace(recallCase.Id) ? null : recallCase.Id.Trim(),
            query,
            hasExpectation,
            hit,
            bestRank,
            hasExpectation ? (bestRank is int rank ? 1d / rank : 0d) : null,
            selected,
            expectations);
    }

    private static IReadOnlyList<MemoryRecallSelectedMemory> MapSelected(
        StableMemorySet stable,
        DateTimeOffset now)
    {
        var selected = new List<MemoryRecallSelectedMemory>(stable.All.Count);
        var rank = 1;

        foreach (var item in stable.Core)
        {
            selected.Add(ToSelected(rank++, item, "Core", MemoryContextDiagnostics.ExplainCoreSelection(item), now));
        }

        foreach (var item in stable.Relevant)
        {
            selected.Add(ToSelected(rank++, item, "Relevant", MemoryContextDiagnostics.ExplainRelevantSelection(item), now));
        }

        return selected;
    }

    private static MemoryRecallSelectedMemory ToSelected(
        int rank,
        RankedMemory item,
        string selectionKind,
        string selectionReason,
        DateTimeOffset now)
    {
        return new MemoryRecallSelectedMemory(
            rank,
            new MemoryContextItem(
                item.Memory.Id,
                item.Memory.Content,
                item.Memory.Type,
                item.Memory.Scope,
                item.Memory.Origin,
                item.Memory.IsPinned,
                item.Memory.Importance,
                item.Memory.Confidence,
                item.Similarity,
                item.Score,
                selectionKind,
                selectionReason,
                item.Memory.SourceSummary,
                item.Memory.ValidFrom,
                item.Memory.ValidUntil),
            MemoryRanker.Describe(item.Memory, item.Similarity, now));
    }

    private async Task<IReadOnlyList<MemoryRecallExpectationResult>> ResolveExpectationsAsync(
        string ownerId,
        Guid conversationId,
        MemoryRecallCase recallCase,
        IReadOnlyList<MemoryRecallSelectedMemory> selected,
        IReadOnlyDictionary<Guid, MemoryRecallSelectedMemory> selectedById,
        IReadOnlyList<float>? queryEmbedding,
        MemoryAiOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var expectations = new List<MemoryRecallExpectationResult>();
        var seenIds = new HashSet<Guid>();

        foreach (var memoryId in recallCase.ExpectedMemoryIds ?? [])
        {
            if (memoryId == Guid.Empty || !seenIds.Add(memoryId))
            {
                continue;
            }

            if (selectedById.TryGetValue(memoryId, out var selectedMemory))
            {
                expectations.Add(FromSelected(selectedMemory, expectedContentContains: null));
                continue;
            }

            expectations.Add(await DiagnoseMissAsync(
                memoryId,
                expectedContentContains: null,
                ownerId,
                conversationId,
                selected,
                queryEmbedding,
                options,
                now,
                cancellationToken));
        }

        foreach (var rawNeedle in recallCase.ExpectedContentContains ?? [])
        {
            if (string.IsNullOrWhiteSpace(rawNeedle))
            {
                continue;
            }

            var needle = rawNeedle.Trim();
            var selectedMemory = selected.FirstOrDefault(item =>
                item.Memory.Content.Contains(needle, StringComparison.OrdinalIgnoreCase));
            if (selectedMemory is not null)
            {
                expectations.Add(FromSelected(selectedMemory, needle));
                continue;
            }

            var lookup = MemoryLookup.ForRetrieval(ownerId, conversationId);
            var active = await memoryStore.GetActiveMemoriesAsync(lookup, cancellationToken);
            var matched = active.FirstOrDefault(memory =>
                memory.Content.Contains(needle, StringComparison.OrdinalIgnoreCase));
            if (matched is null)
            {
                expectations.Add(new MemoryRecallExpectationResult(
                    null,
                    needle,
                    false,
                    null,
                    null,
                    "No matching memory exists.",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null));
                continue;
            }

            if (selectedById.TryGetValue(matched.Id, out selectedMemory))
            {
                expectations.Add(FromSelected(selectedMemory, needle));
                continue;
            }

            expectations.Add(await DiagnoseMissAsync(
                matched.Id,
                needle,
                ownerId,
                conversationId,
                selected,
                queryEmbedding,
                options,
                now,
                cancellationToken));
        }

        return expectations;
    }

    private static MemoryRecallExpectationResult FromSelected(
        MemoryRecallSelectedMemory selected,
        string? expectedContentContains)
    {
        return new MemoryRecallExpectationResult(
            selected.Memory.Id,
            expectedContentContains,
            true,
            selected.Rank,
            selected.Memory.SelectionKind,
            null,
            selected.Memory.Content,
            selected.Memory.Type,
            selected.Memory.Scope,
            selected.Memory.Score,
            selected.Memory.Similarity,
            selected.ScoreBreakdown);
    }

    private async Task<MemoryRecallExpectationResult> DiagnoseMissAsync(
        Guid memoryId,
        string? expectedContentContains,
        string ownerId,
        Guid conversationId,
        IReadOnlyList<MemoryRecallSelectedMemory> selected,
        IReadOnlyList<float>? queryEmbedding,
        MemoryAiOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var memory = await memoryStore.GetMemoryAsync(memoryId, cancellationToken);
        if (memory is null || !string.Equals(memory.OwnerId, ownerId, StringComparison.Ordinal))
        {
            return new MemoryRecallExpectationResult(
                memoryId,
                expectedContentContains,
                false,
                null,
                null,
                "No matching memory exists.",
                memory?.Content,
                memory?.Type,
                memory?.Scope,
                null,
                null,
                null);
        }

        if (!memory.IsEffective(now))
        {
            var inactive = MemoryRanker.Describe(memory, similarity: null, now);
            return new MemoryRecallExpectationResult(
                memory.Id,
                expectedContentContains,
                false,
                null,
                null,
                "Memory is not active or is outside its validity window.",
                memory.Content,
                memory.Type,
                memory.Scope,
                inactive.Score,
                null,
                inactive);
        }

        double? similarity = null;
        if (queryEmbedding is not null && memory.Embedding is { Length: > 0 })
        {
            similarity = MemoryRanker.EmbeddingSimilarity(queryEmbedding, memory.Embedding);
        }

        var breakdown = MemoryRanker.Describe(memory, similarity, now);
        var missReason = DescribeMissReason(
            memory,
            conversationId,
            selected,
            similarity,
            breakdown.Score,
            queryEmbedding,
            options);

        return new MemoryRecallExpectationResult(
            memory.Id,
            expectedContentContains,
            false,
            null,
            null,
            missReason,
            memory.Content,
            memory.Type,
            memory.Scope,
            breakdown.Score,
            similarity,
            breakdown);
    }

    private static string DescribeMissReason(
        MemoryEntity memory,
        Guid conversationId,
        IReadOnlyList<MemoryRecallSelectedMemory> selected,
        double? similarity,
        double score,
        IReadOnlyList<float>? queryEmbedding,
        MemoryAiOptions options)
    {
        var coreLimit = options.CoreMemoryLimit <= 0 ? 12 : options.CoreMemoryLimit;
        if (MemoryStability.IsCore(memory, options.CoreImportanceThreshold)
            && selected.Count(item => item.Memory.SelectionKind == "Core") >= coreLimit)
        {
            return $"Eligible as core memory but dropped by CoreMemoryLimit ({coreLimit}).";
        }

        if (queryEmbedding is not null && memory.Embedding is not { Length: > 0 })
        {
            return "Memory has no embedding, so semantic search could not retrieve it.";
        }

        if (!memory.IsPinned
            && queryEmbedding is not null
            && similarity is not null
            && similarity < options.MinRelevantSimilarity)
        {
            return $"Below MinRelevantSimilarity ({similarity.Value:0.00} < {options.MinRelevantSimilarity:0.00}).";
        }

        if (!memory.IsPinned && score < options.MinRelevantScore)
        {
            return $"Below MinRelevantScore ({score:0.00} < {options.MinRelevantScore:0.00}).";
        }

        if (memory.Scope == MemoryScope.Conversation && memory.ConversationId != conversationId)
        {
            return "Conversation-scoped memory belongs to a different thread.";
        }

        var maxRelevant = options.MaxRelevantMemories <= 0 ? 8 : Math.Min(options.MaxRelevantMemories, 40);
        if (selected.Count(item => item.Memory.SelectionKind == "Relevant") >= maxRelevant)
        {
            return $"Passed retrieval thresholds but was outside MaxRelevantMemories ({maxRelevant}).";
        }

        return "Not selected by current retrieval ranking.";
    }
}
