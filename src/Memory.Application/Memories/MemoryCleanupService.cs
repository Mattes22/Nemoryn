namespace Memory.Application.Memories;

using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.Persistence;
using Memory.Application.Configuration;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

internal sealed class MemoryCleanupService(
    IMemoryStore memoryStore,
    IOptions<MemoryAiOptions> memoryAiOptions) : IMemoryCleanupService
{
    public async Task<MemoryCleanupPreviewResponse> PreviewAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = NormalizeOwnerId(ownerId);
        var memories = await memoryStore.GetActiveMemoriesForOwnerAsync(normalizedOwnerId, cancellationToken);
        var suggestions = BuildSuggestions(normalizedOwnerId, memories);

        return new MemoryCleanupPreviewResponse(
            normalizedOwnerId,
            memories.Count,
            suggestions);
    }

    public async Task<MemoryCleanupApplyResponse> ApplyAsync(
        string ownerId,
        MemoryCleanupApplyRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = NormalizeOwnerId(ownerId);
        var requestedIds = request.MemoryIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (requestedIds.Length == 0)
        {
            return new MemoryCleanupApplyResponse(0, 0, []);
        }

        var activeMemories = await memoryStore.GetActiveMemoriesForOwnerAsync(normalizedOwnerId, cancellationToken);
        var allowedIds = BuildSuggestions(normalizedOwnerId, activeMemories)
            .Select(suggestion => suggestion.MemoryId)
            .ToHashSet();
        var memories = await memoryStore.GetMemoriesByIdsForOwnerAsync(
            normalizedOwnerId,
            requestedIds,
            cancellationToken);
        var archived = new List<MemoryResponse>();

        foreach (var memory in memories)
        {
            if (!allowedIds.Contains(memory.Id))
            {
                continue;
            }

            memory.Forget();
            memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForMemory(
                MemoryAuditAction.Forgotten,
                memory,
                MemoryAuditActorKind.Cleanup,
                "Memory archived by cleanup."));
            archived.Add(MemoryService.ToResponse(memory));
        }

        await memoryStore.SaveChangesAsync(cancellationToken);

        return new MemoryCleanupApplyResponse(
            requestedIds.Length,
            archived.Count,
            archived);
    }

    private IReadOnlyList<MemoryCleanupSuggestion> BuildSuggestions(
        string ownerId,
        IReadOnlyList<MemoryEntity> memories)
    {
        var suggestions = new Dictionary<Guid, MemoryCleanupSuggestion>();

        foreach (var memory in memories.Where(IsCleanupEligible))
        {
            AddFirstSuggestion(suggestions, AnalyzeSingleMemory(ownerId, memory));
        }

        foreach (var duplicate in FindDuplicateSuggestions(memories))
        {
            AddFirstSuggestion(suggestions, duplicate);
        }

        return suggestions.Values
            .OrderBy(suggestion => Severity(suggestion.ReasonCode))
            .ThenByDescending(suggestion => suggestion.Memory.Importance)
            .ThenByDescending(suggestion => suggestion.Memory.Confidence)
            .ToArray();
    }

    private MemoryCleanupSuggestion? AnalyzeSingleMemory(string ownerId, MemoryEntity memory)
    {
        if (ExtractedMemoryMetadataFilter.ShouldDiscard(
            memory.Content,
            ownerId,
            memory.ConversationId,
            externalId: null))
        {
            return Suggest(
                memory,
                "technical_metadata",
                "Paměť vypadá jako interní metadata nebo fakt odvozený z interních metadat.");
        }

        if (LooksLikeTestMemory(memory.Content))
        {
            return Suggest(
                memory,
                "test_data",
                "Paměť vypadá jako testovací nebo smoke-test záznam.");
        }

        var options = memoryAiOptions.Value;
        if (memory.Confidence < options.MinPersistConfidence
            || memory.Importance < options.MinPersistImportance)
        {
            return Suggest(
                memory,
                "low_signal",
                "Paměť je pod prahem, od kterého by se dnes už neukládala.");
        }

        return null;
    }

    private IEnumerable<MemoryCleanupSuggestion> FindDuplicateSuggestions(IReadOnlyList<MemoryEntity> memories)
    {
        var eligible = memories.Where(IsCleanupEligible).ToArray();
        var exactDuplicates = FindExactDuplicateSuggestions(eligible).ToArray();

        foreach (var suggestion in exactDuplicates)
        {
            yield return suggestion;
        }

        var exactDuplicateIds = exactDuplicates
            .Select(suggestion => suggestion.MemoryId)
            .ToHashSet();

        foreach (var suggestion in FindSimilarDuplicateSuggestions(
            eligible.Where(memory => !exactDuplicateIds.Contains(memory.Id)).ToArray()))
        {
            yield return suggestion;
        }
    }

    private static IEnumerable<MemoryCleanupSuggestion> FindExactDuplicateSuggestions(
        IReadOnlyList<MemoryEntity> memories)
    {
        return memories
            .GroupBy(memory => new
            {
                memory.Scope,
                memory.Type,
                Content = NormalizeContent(memory.Content)
            })
            .Where(group => group.Count() > 1)
            .SelectMany(group => WeakerThanKept(group, "Paměť je duplicitní vůči jiné aktivní paměti stejného typu a scope."));
    }

    private IEnumerable<MemoryCleanupSuggestion> FindSimilarDuplicateSuggestions(
        IReadOnlyList<MemoryEntity> memories)
    {
        var threshold = memoryAiOptions.Value.SimilarityThreshold;
        var grouped = memories
            .Where(memory => memory.Embedding is { Length: > 0 })
            .GroupBy(memory => new { memory.Scope, memory.Type });

        foreach (var group in grouped)
        {
            var ranked = group
                .OrderByDescending(memory => memory.Importance)
                .ThenByDescending(memory => memory.Confidence)
                .ThenByDescending(memory => memory.CreatedAt)
                .ToArray();
            var kept = new List<MemoryEntity>();

            foreach (var memory in ranked)
            {
                var similarKept = kept.Any(existing =>
                    MemoryRanker.EmbeddingSimilarity(existing.Embedding!, memory.Embedding!) >= threshold);

                if (similarKept)
                {
                    yield return Suggest(
                        memory,
                        "duplicate",
                        "Paměť je významově duplicitní vůči silnější aktivní paměti stejného typu a scope.");
                    continue;
                }

                kept.Add(memory);
            }
        }
    }

    private static IEnumerable<MemoryCleanupSuggestion> WeakerThanKept(
        IEnumerable<MemoryEntity> group,
        string reason)
    {
        var ranked = group
            .OrderByDescending(memory => memory.IsPinned)
            .ThenByDescending(memory => memory.Origin == MemoryOrigin.Explicit)
            .ThenByDescending(memory => memory.Importance)
            .ThenByDescending(memory => memory.Confidence)
            .ThenByDescending(memory => memory.CreatedAt)
            .ToArray();

        return ranked
            .Skip(1)
            .Select(memory => Suggest(memory, "duplicate", reason));
    }

    private static bool IsCleanupEligible(MemoryEntity memory)
    {
        return memory.Status == MemoryStatus.Active
            && memory.Origin == MemoryOrigin.Inferred
            && !memory.IsPinned;
    }

    private static void AddFirstSuggestion(
        IDictionary<Guid, MemoryCleanupSuggestion> suggestions,
        MemoryCleanupSuggestion? suggestion)
    {
        if (suggestion is not null && !suggestions.ContainsKey(suggestion.MemoryId))
        {
            suggestions.Add(suggestion.MemoryId, suggestion);
        }
    }

    private static MemoryCleanupSuggestion Suggest(MemoryEntity memory, string reasonCode, string reason)
    {
        return new MemoryCleanupSuggestion(
            memory.Id,
            reasonCode,
            reason,
            MemoryService.ToResponse(memory));
    }

    private static bool LooksLikeTestMemory(string content)
    {
        var normalized = NormalizeContent(content);
        return normalized.Contains("smoke test", StringComparison.Ordinal)
            || normalized.Contains("test pameti", StringComparison.Ordinal)
            || normalized.Contains("test memory", StringComparison.Ordinal);
    }

    private static string NormalizeContent(string content)
    {
        var normalized = content.Trim().TrimEnd('.').ToLowerInvariant();
        normalized = normalized
            .Replace('ě', 'e')
            .Replace('š', 's')
            .Replace('č', 'c')
            .Replace('ř', 'r')
            .Replace('ž', 'z')
            .Replace('ý', 'y')
            .Replace('á', 'a')
            .Replace('í', 'i')
            .Replace('é', 'e')
            .Replace('ú', 'u')
            .Replace('ů', 'u')
            .Replace('ť', 't')
            .Replace('ď', 'd')
            .Replace('ň', 'n');

        if (normalized.StartsWith("the user ", StringComparison.Ordinal))
        {
            normalized = normalized["the user ".Length..];
        }
        else if (normalized.StartsWith("user's ", StringComparison.Ordinal))
        {
            normalized = normalized["user's ".Length..];
        }
        else if (normalized.StartsWith("user ", StringComparison.Ordinal))
        {
            normalized = normalized["user ".Length..];
        }

        return normalized;
    }

    private static int Severity(string reasonCode)
    {
        return reasonCode switch
        {
            "technical_metadata" => 0,
            "duplicate" => 1,
            "test_data" => 2,
            "low_signal" => 3,
            _ => 10
        };
    }

    private static string NormalizeOwnerId(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(ownerId));
        }

        return ownerId.Trim();
    }
}
