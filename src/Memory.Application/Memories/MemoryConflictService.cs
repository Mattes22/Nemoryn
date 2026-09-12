namespace Memory.Application.Memories;

using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.AI;
using Memory.Application.Abstractions.Persistence;
using Memory.Application.Configuration;
using Memory.Application.Exceptions;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

internal sealed class MemoryConflictService(
    IMemoryStore memoryStore,
    IEmbeddingProvider embeddingProvider,
    IOptions<MemoryAiOptions> memoryAiOptions) : IMemoryConflictService
{
    public async Task<IReadOnlyList<MemoryConflictResponse>> GetPendingForConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await memoryStore.GetConversationAsync(conversationId, cancellationToken)
            ?? throw new NotFoundException("Conversation was not found.");

        var conflicts = await memoryStore.GetPendingMemoryConflictsForConversationAsync(
            conversation.Id,
            cancellationToken);

        return await ToResponsesAsync(conflicts, cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryConflictResponse>> GetPendingForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(ownerId));
        }

        var conflicts = await memoryStore.GetPendingMemoryConflictsForOwnerAsync(ownerId.Trim(), cancellationToken);
        return await ToResponsesAsync(conflicts, cancellationToken);
    }

    public async Task<MemoryResponse> AcceptCandidateAsync(Guid conflictId, CancellationToken cancellationToken = default)
    {
        var conflict = await GetRequiredPendingConflictAsync(conflictId, cancellationToken);
        var candidate = conflict.Candidate;
        var existing = conflict.ConflictingMemory;
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
            MemoryOrigin.Explicit);

        await TrySetEmbeddingAsync(memory, cancellationToken);

        var pendingConflicts = await memoryStore.GetPendingMemoryConflictsForCandidateAsync(
            candidate.Id,
            cancellationToken);

        await memoryStore.ExecuteInTransactionAsync(async () =>
        {
            memoryStore.AddMemory(memory);
            existing.Supersede(memory.Id);
            candidate.Promote(memory.Id);
            conflict.MarkCandidateAccepted();
            memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForMemory(
                MemoryAuditAction.Created,
                memory,
                MemoryAuditActorKind.User,
                "Conflict accepted; candidate became memory.",
                candidateId: candidate.Id,
                conflictId: conflict.Id));
            memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForCandidate(
                MemoryAuditAction.Promoted,
                candidate,
                MemoryAuditActorKind.User,
                "Candidate promoted by accepting the conflict.",
                memory.Id,
                conflict.Id));
            memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForConflict(
                MemoryAuditAction.ConflictAccepted,
                conflict,
                MemoryAuditActorKind.User,
                "User accepted the candidate.",
                memory.Id));
            memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForMemory(
                MemoryAuditAction.Superseded,
                existing,
                MemoryAuditActorKind.User,
                "Existing memory superseded by accepted candidate.",
                details: memory.Id.ToString(),
                candidateId: candidate.Id,
                conflictId: conflict.Id));
            foreach (var sibling in pendingConflicts)
            {
                if (sibling.Id != conflict.Id)
                {
                    sibling.MarkExistingKept();
                    memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForConflict(
                        MemoryAuditAction.ConflictKept,
                        sibling,
                        MemoryAuditActorKind.User,
                        "Sibling conflict closed after another candidate was accepted."));
                }
            }

            await memoryStore.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

        return MemoryService.ToResponse(memory);
    }

    public async Task<MemoryConflictResponse> KeepExistingAsync(Guid conflictId, CancellationToken cancellationToken = default)
    {
        var conflict = await GetRequiredPendingConflictAsync(conflictId, cancellationToken);
        var pendingConflicts = await memoryStore.GetPendingMemoryConflictsForCandidateAsync(
            conflict.CandidateId,
            cancellationToken);

        await memoryStore.ExecuteInTransactionAsync(async () =>
        {
            conflict.Candidate.Reject();
            memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForCandidate(
                MemoryAuditAction.Rejected,
                conflict.Candidate,
                MemoryAuditActorKind.User,
                "Candidate rejected by keeping the existing memory.",
                conflictId: conflict.Id));
            foreach (var pending in pendingConflicts)
            {
                pending.MarkExistingKept();
                memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForConflict(
                    MemoryAuditAction.ConflictKept,
                    pending,
                    MemoryAuditActorKind.User,
                    "User kept the existing memory."));
            }

            await memoryStore.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

        return await ToResponseAsync(conflict, cancellationToken);
    }

    private async Task<MemoryConflict> GetRequiredPendingConflictAsync(
        Guid conflictId,
        CancellationToken cancellationToken)
    {
        var conflict = await memoryStore.GetMemoryConflictAsync(conflictId, cancellationToken)
            ?? throw new NotFoundException("Memory conflict was not found.");

        if (conflict.Status != MemoryConflictStatus.Pending)
        {
            throw new ArgumentException("Only a pending memory conflict can be resolved.", nameof(conflictId));
        }

        if (conflict.Candidate.Status != MemoryCandidateStatus.Pending)
        {
            throw new ArgumentException("Only a conflict with a pending candidate can be resolved.", nameof(conflictId));
        }

        if (conflict.ConflictingMemory.Status != MemoryStatus.Active)
        {
            throw new ArgumentException("Only a conflict with an active memory can be resolved.", nameof(conflictId));
        }

        return conflict;
    }

    private async Task<IReadOnlyList<MemoryConflictResponse>> ToResponsesAsync(
        IReadOnlyList<MemoryConflict> conflicts,
        CancellationToken cancellationToken)
    {
        var responses = new List<MemoryConflictResponse>(conflicts.Count);
        foreach (var conflict in conflicts)
        {
            responses.Add(await ToResponseAsync(conflict, cancellationToken));
        }

        return responses;
    }

    private async Task<MemoryConflictResponse> ToResponseAsync(
        MemoryConflict conflict,
        CancellationToken cancellationToken)
    {
        var evidence = await memoryStore.GetMemoryEvidenceAsync(conflict.CandidateId, cancellationToken);

        return new MemoryConflictResponse(
            conflict.Id,
            conflict.OwnerId,
            conflict.ConversationId,
            conflict.Status,
            conflict.Confidence,
            conflict.Reason,
            ToCandidateResponse(conflict.Candidate, evidence),
            MemoryService.ToResponse(conflict.ConflictingMemory),
            conflict.CreatedAt,
            conflict.UpdatedAt);
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

    private static MemoryCandidateResponse ToCandidateResponse(
        MemoryCandidate candidate,
        IReadOnlyList<MemoryEvidence> evidence)
    {
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
            evidence.Select(ToEvidenceResponse).ToArray());
    }

    private static MemoryEvidenceResponse ToEvidenceResponse(MemoryEvidence evidence)
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
