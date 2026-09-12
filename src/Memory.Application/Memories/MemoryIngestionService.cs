namespace Memory.Application.Memories;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.AI;
using Memory.Application.Abstractions.Persistence;
using Memory.Application.Configuration;
using Memory.Domain.Conversations;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

internal sealed class MemoryIngestionService(
    IMemoryStore memoryStore,
    IMemoryExtractor memoryExtractor,
    IEmbeddingProvider embeddingProvider,
    IContradictionDetector contradictionDetector,
    IOptions<MemoryAiOptions> memoryAiOptions,
    ILogger<MemoryIngestionService> logger) : IMemoryIngestionService
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var job = await memoryStore.ClaimNextIngestionJobAsync(cancellationToken);
        if (job is null)
        {
            return false;
        }

        logger.LogInformation(
            "Ingesting memories for conversation {ConversationId} and message {MessageId}.",
            job.ConversationId,
            job.MessageId);

        try
        {
            if (!memoryExtractor.IsAvailable)
            {
                job.MarkSkipped("Memory extractor is not configured.");
            }
            else
            {
                var ingested = await IngestFromMessageAsync(job.ConversationId, job.MessageId, cancellationToken);
                if (ingested)
                {
                    job.MarkSucceeded();
                }
                else
                {
                    job.MarkSkipped("Conversation or source message was not found.");
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Memory ingestion failed for conversation {ConversationId} and message {MessageId}.",
                job.ConversationId,
                job.MessageId);
            job.MarkFailed(exception.Message);
        }

        await memoryStore.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> IngestFromMessageAsync(
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        if (!memoryExtractor.IsAvailable)
        {
            return false;
        }

        var conversation = await memoryStore.GetConversationAsync(conversationId, cancellationToken);
        var sourceMessage = await memoryStore.GetMessageAsync(messageId, cancellationToken);

        if (conversation is null || sourceMessage is null || sourceMessage.ConversationId != conversationId)
        {
            return false;
        }

        var window = Math.Max(1, memoryAiOptions.Value.RecentMessageWindow);
        var messages = await memoryStore.GetRecentMessagesAsync(conversationId, window, cancellationToken);
        var candidates = await memoryExtractor.ExtractAsync(conversation, messages, cancellationToken);

        var persistFailures = 0;
        foreach (var candidate in candidates)
        {
            try
            {
                await PersistCandidateAsync(conversation, sourceMessage.Id, candidate, cancellationToken);
            }
            catch (Exception exception)
            {
                persistFailures++;
                logger.LogWarning(
                    exception,
                    "Skipping extracted memory candidate for conversation {ConversationId}.",
                    conversationId);
            }
        }

        if (candidates.Count > 0 && persistFailures == candidates.Count)
        {
            throw new InvalidOperationException(
                $"Memory ingestion failed for all {candidates.Count} extracted candidates.");
        }

        return true;
    }

    private async Task PersistCandidateAsync(
        Conversation conversation,
        Guid sourceMessageId,
        ExtractedMemoryCandidate candidate,
        CancellationToken cancellationToken)
    {
        if (ExtractedMemoryMetadataFilter.ShouldDiscard(
            candidate.Content,
            conversation.OwnerId,
            conversation.Id,
            conversation.ExternalId))
        {
            logger.LogInformation(
                "Dropped extracted memory candidate because it looks like internal metadata for conversation {ConversationId}.",
                conversation.Id);
            return;
        }

        var ownerId = conversation.OwnerId;
        var conversationId = conversation.Id;
        var options = memoryAiOptions.Value;
        if (candidate.Confidence < options.MinPersistConfidence
            || candidate.Importance < options.MinPersistImportance)
        {
            return;
        }

        var proposed = new MemoryCandidate(
            ownerId,
            conversationId,
            candidate.Scope,
            candidate.Content,
            candidate.Type,
            candidate.Importance,
            candidate.Confidence,
            candidate.ValidFrom,
            candidate.ValidUntil);

        var duplicate = await memoryStore.GetActiveMemoryByFingerprintAsync(proposed.Fingerprint, cancellationToken);
        if (duplicate is not null)
        {
            return;
        }

        var pending = await memoryStore.GetPendingMemoryCandidateByFingerprintAsync(proposed.Fingerprint, cancellationToken);
        var memoryCandidate = pending ?? proposed;
        var evidence = new MemoryEvidence(
            memoryCandidate.Id,
            conversationId,
            sourceMessageId,
            candidate.SourceSummary);
        var evidenceExists = await memoryStore.MemoryEvidenceExistsAsync(
            memoryCandidate.Id,
            sourceMessageId,
            cancellationToken);
        var effectiveImportance = evidenceExists
            ? memoryCandidate.Importance
            : Math.Max(memoryCandidate.Importance, candidate.Importance);
        var effectiveConfidence = evidenceExists
            ? memoryCandidate.Confidence
            : Math.Max(memoryCandidate.Confidence, candidate.Confidence);
        var effectiveEvidenceCount = evidenceExists
            ? memoryCandidate.EvidenceCount
            : memoryCandidate.EvidenceCount + 1;

        if (!ShouldPromote(effectiveEvidenceCount, effectiveImportance, effectiveConfidence, options))
        {
            await memoryStore.ExecuteInTransactionAsync(async () =>
            {
                if (pending is null)
                {
                    memoryStore.AddMemoryCandidate(memoryCandidate);
                }

                AddEvidenceIfNeeded(memoryCandidate, evidence, candidate, evidenceExists);
                await memoryStore.SaveChangesAsync(cancellationToken);
                return true;
            }, cancellationToken);
            return;
        }

        var memory = new MemoryEntity(
            ownerId,
            conversationId,
            memoryCandidate.Scope,
            memoryCandidate.Content,
            memoryCandidate.Type,
            effectiveImportance,
            effectiveConfidence,
            sourceMessageId,
            candidate.SourceSummary,
            memoryCandidate.ValidFrom,
            memoryCandidate.ValidUntil);

        MemoryEntity? similar = null;
        double similarity = 0;
        IReadOnlyList<MemorySimilarity> matches = [];
        var lookup = memoryCandidate.Scope == MemoryScope.User
            ? MemoryLookup.ForUser(ownerId)
            : MemoryLookup.ForConversation(ownerId, conversationId);

        if (embeddingProvider.IsAvailable)
        {
            var embedding = await embeddingProvider.EmbedAsync(memory.Content, cancellationToken);
            EnsureEmbeddingDimensions(embedding);
            memory.SetEmbedding(embedding);

            matches = await memoryStore.SearchActiveMemoriesAsync(
                lookup,
                embedding,
                NormalizeContradictionCandidateLimit(options),
                cancellationToken);

            if (matches.Count > 0)
            {
                similar = matches[0].Memory;
                similarity = MemoryRanker.ToSimilarity(matches[0].Distance);
            }
        }

        var contradiction = await DetectContradictionAsync(
            memoryCandidate,
            candidate,
            matches,
            options,
            cancellationToken);
        if (contradiction is not null)
        {
            var existingConflict = await memoryStore.GetPendingMemoryConflictAsync(
                contradiction.CandidateId,
                contradiction.ConflictingMemoryId,
                cancellationToken);

            await memoryStore.ExecuteInTransactionAsync(async () =>
            {
                if (pending is null)
                {
                    memoryStore.AddMemoryCandidate(memoryCandidate);
                }

                AddEvidenceIfNeeded(memoryCandidate, evidence, candidate, evidenceExists);
                if (existingConflict is null)
                {
                    memoryStore.AddMemoryConflict(contradiction);
                    memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForConflict(
                        MemoryAuditAction.ConflictOpened,
                        contradiction,
                        MemoryAuditActorKind.Ingestion,
                        contradiction.Reason));
                }

                await memoryStore.SaveChangesAsync(cancellationToken);
                return true;
            }, cancellationToken);
            return;
        }

        if (similar is not null
            && similar.Type == memory.Type
            && similar.Scope == memory.Scope
            && string.Equals(similar.Content, memory.Content, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await memoryStore.ExecuteInTransactionAsync(async () =>
        {
            var threshold = memoryAiOptions.Value.SimilarityThreshold;
            if (pending is null)
            {
                memoryStore.AddMemoryCandidate(memoryCandidate);
            }

            AddEvidenceIfNeeded(memoryCandidate, evidence, candidate, evidenceExists);

            if (similar is not null
                && similar.IsEffective(DateTimeOffset.UtcNow)
                && similar.Type == memory.Type
                && similar.Scope == memory.Scope
                && similarity >= threshold)
            {
                if (!similar.CanBeSupersededBy(memory))
                {
                    var existingConflict = await memoryStore.GetPendingMemoryConflictAsync(
                        memoryCandidate.Id,
                        similar.Id,
                        cancellationToken);

                    if (existingConflict is null)
                    {
                        var opened = new MemoryConflict(
                            memoryCandidate.OwnerId,
                            memoryCandidate.ConversationId,
                            memoryCandidate.Id,
                            similar.Id,
                            effectiveConfidence,
                            "Candidate is similar to an active memory but cannot automatically supersede it.");
                        memoryStore.AddMemoryConflict(opened);
                        memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForConflict(
                            MemoryAuditAction.ConflictOpened,
                            opened,
                            MemoryAuditActorKind.Ingestion,
                            opened.Reason));
                    }
                }
                else
                {
                    RecordInferredPromotion(memory, memoryCandidate, similar);
                }
            }
            else
            {
                RecordInferredPromotion(memory, memoryCandidate, similar: null);
            }

            await memoryStore.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    private void RecordInferredPromotion(
        MemoryEntity memory,
        MemoryCandidate memoryCandidate,
        MemoryEntity? similar)
    {
        memoryStore.AddMemory(memory);
        memoryCandidate.Promote(memory.Id);
        memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForMemory(
            MemoryAuditAction.Created,
            memory,
            MemoryAuditActorKind.Ingestion,
            "Inferred memory auto-promoted.",
            candidateId: memoryCandidate.Id));
        memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForCandidate(
            MemoryAuditAction.Promoted,
            memoryCandidate,
            MemoryAuditActorKind.Ingestion,
            "Candidate auto-promoted by ingestion.",
            memory.Id));
        if (similar is not null)
        {
            similar.Supersede(memory.Id);
            memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForMemory(
                MemoryAuditAction.Superseded,
                similar,
                MemoryAuditActorKind.Ingestion,
                "Active memory superseded by inferred candidate.",
                details: memory.Id.ToString(),
                candidateId: memoryCandidate.Id));
        }
    }

    private static bool ShouldPromote(
        int evidenceCount,
        decimal importance,
        decimal confidence,
        MemoryAiOptions options)
    {
        var evidenceThreshold = options.AutoPromoteEvidenceCount <= 0
            ? 2
            : options.AutoPromoteEvidenceCount;

        if (evidenceCount >= evidenceThreshold)
        {
            return true;
        }

        return confidence >= options.AutoPromoteConfidence
            && importance >= options.AutoPromoteImportance;
    }

    private void AddEvidenceIfNeeded(
        MemoryCandidate memoryCandidate,
        MemoryEvidence evidence,
        ExtractedMemoryCandidate extractedCandidate,
        bool evidenceExists)
    {
        if (evidenceExists)
        {
            return;
        }

        memoryCandidate.AddEvidence(extractedCandidate.Importance, extractedCandidate.Confidence);
        memoryStore.AddMemoryEvidence(evidence);
    }

    private async Task<MemoryConflict?> DetectContradictionAsync(
        MemoryCandidate memoryCandidate,
        ExtractedMemoryCandidate candidate,
        IReadOnlyList<MemorySimilarity> matches,
        MemoryAiOptions options,
        CancellationToken cancellationToken)
    {
        if (!contradictionDetector.IsAvailable || matches.Count == 0)
        {
            return null;
        }

        var relevantMemories = matches
            .Select(match => match.Memory)
            .Where(memory => memory.Type == candidate.Type && memory.Scope == candidate.Scope)
            .ToArray();

        if (relevantMemories.Length == 0)
        {
            return null;
        }

        var decision = await contradictionDetector.DetectAsync(candidate, relevantMemories, cancellationToken);
        if (decision.Relation != MemoryContradictionRelation.Contradicts
            || decision.ConflictingMemoryId is null
            || decision.Confidence < options.ContradictionConfidenceThreshold)
        {
            return null;
        }

        var conflictingMemory = relevantMemories.FirstOrDefault(memory => memory.Id == decision.ConflictingMemoryId);
        if (conflictingMemory is null)
        {
            return null;
        }

        return new MemoryConflict(
            memoryCandidate.OwnerId,
            memoryCandidate.ConversationId,
            memoryCandidate.Id,
            conflictingMemory.Id,
            decision.Confidence,
            decision.Reason);
    }

    private static int NormalizeContradictionCandidateLimit(MemoryAiOptions options)
    {
        return options.ContradictionCandidateLimit <= 0
            ? 5
            : Math.Min(options.ContradictionCandidateLimit, 20);
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
}
