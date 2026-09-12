namespace Memory.Application.Memories;

using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.AI;
using Memory.Application.Abstractions.Persistence;
using Memory.Application.Configuration;
using Memory.Application.Exceptions;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

internal sealed class MemoryCandidateService(
    IMemoryStore memoryStore,
    IEmbeddingProvider embeddingProvider,
    IOptions<MemoryAiOptions> memoryAiOptions) : IMemoryCandidateService
{
    public async Task<IReadOnlyList<MemoryCandidateResponse>> GetPendingForConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await memoryStore.GetConversationAsync(conversationId, cancellationToken)
            ?? throw new NotFoundException("Conversation was not found.");

        var candidates = await memoryStore.GetPendingMemoryCandidatesForConversationAsync(
            conversation.Id,
            cancellationToken);

        return await ToResponsesAsync(candidates, cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryCandidateResponse>> GetPendingForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(ownerId));
        }

        var candidates = await memoryStore.GetPendingMemoryCandidatesForOwnerAsync(
            ownerId.Trim(),
            cancellationToken);

        return await ToResponsesAsync(candidates, cancellationToken);
    }

    public async Task<MemoryResponse> PromoteAsync(
        Guid candidateId,
        PromoteMemoryCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        var candidate = await memoryStore.GetMemoryCandidateAsync(candidateId, cancellationToken)
            ?? throw new NotFoundException("Memory candidate was not found.");

        EnsurePending(candidate);

        var pendingConflicts = await memoryStore.GetPendingMemoryConflictsForCandidateAsync(
            candidate.Id,
            cancellationToken);
        if (pendingConflicts.Count > 0)
        {
            throw new ConflictException("This candidate has a pending memory conflict and must be resolved there.");
        }

        var duplicate = await memoryStore.GetActiveMemoryByFingerprintAsync(candidate.Fingerprint, cancellationToken);
        if (duplicate is not null)
        {
            throw new ConflictException("An active memory with the same content already exists.");
        }

        var evidence = await memoryStore.GetMemoryEvidenceAsync(candidate.Id, cancellationToken);
        var latestEvidence = evidence.OrderByDescending(item => item.CreatedAt).FirstOrDefault();
        var memory = new MemoryEntity(
            candidate.OwnerId,
            candidate.ConversationId,
            candidate.Scope,
            candidate.Content,
            candidate.Type,
            candidate.Importance,
            candidate.Confidence,
            latestEvidence?.SourceMessageId,
            latestEvidence?.SourceSummary,
            candidate.ValidFrom,
            candidate.ValidUntil,
            MemoryOrigin.Explicit,
            request.Pin);

        await TrySetEmbeddingAsync(memory, cancellationToken);

        var similar = await FindSimilarActiveMemoryAsync(memory, cancellationToken);
        if (similar is not null && !similar.CanBeSupersededBy(memory))
        {
            var existingConflict = await memoryStore.GetPendingMemoryConflictAsync(
                candidate.Id,
                similar.Id,
                cancellationToken);
            if (existingConflict is null)
            {
                var opened = new MemoryConflict(
                    candidate.OwnerId,
                    candidate.ConversationId,
                    candidate.Id,
                    similar.Id,
                    candidate.Confidence,
                    "Candidate is similar to an active memory that cannot be superseded automatically.");
                memoryStore.AddMemoryConflict(opened);
                memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForConflict(
                    MemoryAuditAction.ConflictOpened,
                    opened,
                    MemoryAuditActorKind.User,
                    opened.Reason));
                await memoryStore.SaveChangesAsync(cancellationToken);
            }

            throw new ConflictException("This candidate conflicts with an active memory that cannot be superseded automatically.");
        }

        await memoryStore.ExecuteInTransactionAsync(async () =>
        {
            memoryStore.AddMemory(memory);
            memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForMemory(
                MemoryAuditAction.Created,
                memory,
                MemoryAuditActorKind.User,
                "Candidate promoted.",
                candidateId: candidate.Id));
            memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForCandidate(
                MemoryAuditAction.Promoted,
                candidate,
                MemoryAuditActorKind.User,
                "Candidate promoted by user.",
                memory.Id));
            if (request.Pin)
            {
                memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForMemory(
                    MemoryAuditAction.Pinned,
                    memory,
                    MemoryAuditActorKind.User,
                    "Memory pinned during promote.",
                    candidateId: candidate.Id));
            }

            if (similar is not null)
            {
                similar.Supersede(memory.Id);
                memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForMemory(
                    MemoryAuditAction.Superseded,
                    similar,
                    MemoryAuditActorKind.User,
                    "Active memory superseded by promoted candidate.",
                    details: memory.Id.ToString(),
                    candidateId: candidate.Id));
            }

            candidate.Promote(memory.Id);
            await memoryStore.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

        return MemoryService.ToResponse(memory);
    }

    public async Task<MemoryCandidateResponse> RejectAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var candidate = await memoryStore.GetMemoryCandidateAsync(candidateId, cancellationToken)
            ?? throw new NotFoundException("Memory candidate was not found.");

        EnsurePending(candidate);

        var pendingConflicts = await memoryStore.GetPendingMemoryConflictsForCandidateAsync(
            candidate.Id,
            cancellationToken);

        await memoryStore.ExecuteInTransactionAsync(async () =>
        {
            foreach (var conflict in pendingConflicts)
            {
                conflict.MarkExistingKept();
                memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForConflict(
                    MemoryAuditAction.ConflictKept,
                    conflict,
                    MemoryAuditActorKind.User,
                    "Pending conflict closed because the candidate was rejected."));
            }

            candidate.Reject();
            memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForCandidate(
                MemoryAuditAction.Rejected,
                candidate,
                MemoryAuditActorKind.User,
                "Candidate rejected by user."));
            await memoryStore.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

        return await ToResponseAsync(candidate, cancellationToken);
    }

    private async Task<IReadOnlyList<MemoryCandidateResponse>> ToResponsesAsync(
        IReadOnlyList<MemoryCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var responses = new List<MemoryCandidateResponse>(candidates.Count);
        foreach (var candidate in candidates)
        {
            responses.Add(await ToResponseAsync(candidate, cancellationToken));
        }

        return responses;
    }

    private async Task<MemoryCandidateResponse> ToResponseAsync(
        MemoryCandidate candidate,
        CancellationToken cancellationToken)
    {
        var evidence = await memoryStore.GetMemoryEvidenceAsync(candidate.Id, cancellationToken);

        return new MemoryCandidateResponse(
            candidate.Id,
            candidate.OwnerId,
            candidate.ConversationId,
            candidate.Scope,
            candidate.Content,
            candidate.Type,
            candidate.Status,
            candidate.Importance,
            candidate.Confidence,
            candidate.EvidenceCount,
            candidate.LastEvidenceAt,
            candidate.ValidFrom,
            candidate.ValidUntil,
            candidate.PromotedMemoryId,
            candidate.MergedIntoCandidateId,
            candidate.CreatedAt,
            candidate.UpdatedAt,
            evidence.Select(ToResponse).ToArray());
    }

    private async Task<MemoryEntity?> FindSimilarActiveMemoryAsync(
        MemoryEntity memory,
        CancellationToken cancellationToken)
    {
        if (!embeddingProvider.IsAvailable || memory.Embedding is null)
        {
            return null;
        }

        var lookup = memory.Scope == MemoryScope.User
            ? MemoryLookup.ForUser(memory.OwnerId)
            : MemoryLookup.ForConversation(memory.OwnerId, memory.ConversationId);
        var matches = await memoryStore.SearchActiveMemoriesAsync(
            lookup,
            memory.Embedding,
            Math.Max(1, memoryAiOptions.Value.SearchLimit),
            cancellationToken);
        var threshold = memoryAiOptions.Value.SimilarityThreshold;

        return matches
            .Where(match => match.Memory.Type == memory.Type && match.Memory.Scope == memory.Scope)
            .Where(match => MemoryRanker.ToSimilarity(match.Distance) >= threshold)
            .Select(match => match.Memory)
            .FirstOrDefault();
    }

    private async Task TrySetEmbeddingAsync(MemoryEntity memory, CancellationToken cancellationToken)
    {
        if (!embeddingProvider.IsAvailable)
        {
            return;
        }

        var embedding = await embeddingProvider.EmbedAsync(memory.Content, cancellationToken);
        var expected = memoryAiOptions.Value.EmbeddingDimensions;
        if (embedding.Count != expected)
        {
            throw new InvalidOperationException(
                $"Embedding dimension mismatch. Expected {expected}, received {embedding.Count}.");
        }

        memory.SetEmbedding(embedding);
    }

    private static void EnsurePending(MemoryCandidate candidate)
    {
        if (candidate.Status != MemoryCandidateStatus.Pending)
        {
            throw new ArgumentException("Only a pending memory candidate can be changed.", nameof(candidate));
        }
    }

    private static MemoryEvidenceResponse ToResponse(MemoryEvidence evidence)
    {
        return new MemoryEvidenceResponse(
            evidence.Id,
            evidence.CandidateId,
            evidence.ConversationId,
            evidence.SourceMessageId,
            evidence.SourceSummary,
            evidence.CreatedAt);
    }
}
