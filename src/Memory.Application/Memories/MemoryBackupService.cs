namespace Memory.Application.Memories;

using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.Persistence;
using Memory.Application.Configuration;
using Memory.Domain.Conversations;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

internal sealed class MemoryBackupService(
    IMemoryStore memoryStore,
    IMemoryPolicyService memoryPolicyService,
    IOptions<MemoryAiOptions> memoryAiOptions) : IMemoryBackupService
{
    public const int MaxAuditExport = 200;
    public const int MaxRecentAudit = 50;
    public const int MaxImportIssues = 40;
    public const int MaxRestoredEvidenceCount = 20;

    public async Task<MemoryProfileExport> ExportAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = RequireOwnerId(ownerId);
        var memories = await memoryStore.GetActiveMemoriesForOwnerAsync(normalizedOwnerId, cancellationToken);
        var candidates = await memoryStore.GetPendingMemoryCandidatesForOwnerAsync(
            normalizedOwnerId,
            cancellationToken);
        var conflicts = await memoryStore.GetPendingMemoryConflictsForOwnerAsync(
            normalizedOwnerId,
            cancellationToken);
        var conversations = await LoadReferencedConversationsAsync(
            normalizedOwnerId,
            memories,
            candidates,
            conflicts,
            cancellationToken);
        var auditEntries = await memoryStore.GetMemoryAuditLogsAsync(
            normalizedOwnerId,
            memoryId: null,
            conversationId: null,
            MaxAuditExport,
            cancellationToken);

        return new MemoryProfileExport(
            MemoryProfileExport.FormatVersion,
            DateTimeOffset.UtcNow,
            normalizedOwnerId,
            memoryPolicyService.Get().Policy,
            conversations
                .Select(conversation => new MemoryExportConversation(
                    conversation.Id,
                    conversation.ExternalId,
                    conversation.Title))
                .ToArray(),
            memories.Select(ToExportMemory).ToArray(),
            candidates.Select(ToExportCandidate).ToArray(),
            conflicts.Select(ToExportConflict).ToArray(),
            ToAuditMetadata(auditEntries));
    }

    public async Task<MemoryImportResult> ImportAsync(
        string ownerId,
        MemoryImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var document = request.Document
            ?? throw new ArgumentException("Import document is required.", nameof(request));
        if (!string.Equals(document.Format, MemoryProfileExport.FormatVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Unsupported memory profile format '{document.Format}'. Expected {MemoryProfileExport.FormatVersion}.",
                nameof(request));
        }

        var normalizedOwnerId = RequireOwnerId(ownerId);
        var dryRun = request.DryRun ?? true;
        var skipDuplicates = request.SkipDuplicates ?? true;
        var pinExplicit = request.PinExplicit ?? false;

        if (dryRun)
        {
            return await ImportCoreAsync(
                normalizedOwnerId,
                document,
                dryRun: true,
                skipDuplicates,
                pinExplicit,
                cancellationToken);
        }

        return await memoryStore.ExecuteInTransactionAsync(
            () => ImportCoreAsync(
                normalizedOwnerId,
                document,
                dryRun: false,
                skipDuplicates,
                pinExplicit,
                cancellationToken),
            cancellationToken);
    }

    private async Task<MemoryImportResult> ImportCoreAsync(
        string ownerId,
        MemoryProfileExport document,
        bool dryRun,
        bool skipDuplicates,
        bool pinExplicit,
        CancellationToken cancellationToken)
    {
        var conversationCounts = new Counter();
        var memoryCounts = new Counter();
        var candidateCounts = new Counter();
        var conflictCounts = new Counter();
        var issues = new List<MemoryImportIssue>();
        var conversationMap = new Dictionary<Guid, Guid>();
        var memoryMap = new Dictionary<Guid, Guid>();
        var candidateMap = new Dictionary<Guid, Guid>();
        var createdMemoryFingerprints = new HashSet<string>(StringComparer.Ordinal);
        var createdCandidateFingerprints = new HashSet<string>(StringComparer.Ordinal);
        var createdConflictKeys = new HashSet<string>(StringComparer.Ordinal);
        var conversationsById = (document.Conversations ?? [])
            .GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var item in document.Conversations ?? [])
        {
            await ResolveConversationAsync(
                ownerId,
                item.Id,
                conversationsById,
                conversationMap,
                conversationCounts,
                dryRun,
                cancellationToken);
        }

        foreach (var item in document.Memories ?? [])
        {
            try
            {
                var conversationId = await ResolveConversationAsync(
                    ownerId,
                    item.ConversationId,
                    conversationsById,
                    conversationMap,
                    conversationCounts,
                    dryRun,
                    cancellationToken);
                var fingerprint = MemoryFingerprint.Compute(ownerId, item.Scope, item.Type, item.Content);
                var existing = createdMemoryFingerprints.Contains(fingerprint)
                    ? null
                    : await memoryStore.GetActiveMemoryByFingerprintAsync(fingerprint, cancellationToken);

                if (existing is not null || createdMemoryFingerprints.Contains(fingerprint))
                {
                    if (!skipDuplicates)
                    {
                        memoryCounts.Failed++;
                        AddIssue(issues, "memory", fingerprint, "An active memory with the same fingerprint already exists.");
                        continue;
                    }

                    if (existing is not null)
                    {
                        memoryMap[item.Id] = existing.Id;
                    }

                    memoryCounts.Skipped++;
                    continue;
                }

                createdMemoryFingerprints.Add(fingerprint);
                if (dryRun)
                {
                    memoryMap[item.Id] = Guid.NewGuid();
                }
                else
                {
                    var shouldPin = item.IsPinned || (pinExplicit && item.Origin == MemoryOrigin.Explicit);
                    var memory = new MemoryEntity(
                        ownerId,
                        conversationId,
                        item.Scope,
                        item.Content,
                        item.Type,
                        item.Importance,
                        item.Confidence,
                        sourceSummary: item.SourceSummary,
                        validFrom: item.ValidFrom,
                        validUntil: item.ValidUntil,
                        origin: item.Origin,
                        isPinned: shouldPin);
                    memory.SetSourceMetadataJson(item.SourceMetadataJson);
                    TrySetEmbedding(memory, item.Embedding);
                    memoryStore.AddMemory(memory);
                    memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForMemory(
                        MemoryAuditAction.Created,
                        memory,
                        MemoryAuditActorKind.User,
                        "Imported from memory profile backup."));
                    memoryMap[item.Id] = memory.Id;
                }

                memoryCounts.Created++;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                memoryCounts.Failed++;
                AddIssue(issues, "memory", item.Fingerprint, exception.Message);
            }
        }

        foreach (var item in document.Candidates ?? [])
        {
            try
            {
                var conversationId = await ResolveConversationAsync(
                    ownerId,
                    item.ConversationId,
                    conversationsById,
                    conversationMap,
                    conversationCounts,
                    dryRun,
                    cancellationToken);
                var fingerprint = MemoryFingerprint.Compute(ownerId, item.Scope, item.Type, item.Content);
                var existing = createdCandidateFingerprints.Contains(fingerprint)
                    ? null
                    : await memoryStore.GetPendingMemoryCandidateByFingerprintAsync(fingerprint, cancellationToken);

                if (existing is not null || createdCandidateFingerprints.Contains(fingerprint))
                {
                    if (!skipDuplicates)
                    {
                        candidateCounts.Failed++;
                        AddIssue(issues, "candidate", fingerprint, "A pending candidate with the same fingerprint already exists.");
                        continue;
                    }

                    if (existing is not null)
                    {
                        candidateMap[item.Id] = existing.Id;
                    }

                    candidateCounts.Skipped++;
                    continue;
                }

                createdCandidateFingerprints.Add(fingerprint);
                if (dryRun)
                {
                    candidateMap[item.Id] = Guid.NewGuid();
                }
                else
                {
                    var candidate = new MemoryCandidate(
                        ownerId,
                        conversationId,
                        item.Scope,
                        item.Content,
                        item.Type,
                        item.Importance,
                        item.Confidence,
                        item.ValidFrom,
                        item.ValidUntil);
                    RestoreEvidenceCount(candidate, item.EvidenceCount);
                    memoryStore.AddMemoryCandidate(candidate);
                    memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForCandidate(
                        MemoryAuditAction.Created,
                        candidate,
                        MemoryAuditActorKind.User,
                        "Imported from memory profile backup."));
                    candidateMap[item.Id] = candidate.Id;
                }

                candidateCounts.Created++;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                candidateCounts.Failed++;
                AddIssue(issues, "candidate", item.Fingerprint, exception.Message);
            }
        }

        foreach (var item in document.Conflicts ?? [])
        {
            try
            {
                if (!TryResolveImportedId(item.CandidateId, candidateMap, out var candidateId)
                    || !TryResolveImportedId(item.ConflictingMemoryId, memoryMap, out var memoryId))
                {
                    if (skipDuplicates)
                    {
                        conflictCounts.Skipped++;
                        continue;
                    }

                    conflictCounts.Failed++;
                    AddIssue(
                        issues,
                        "conflict",
                        item.CandidateFingerprint,
                        "Candidate or conflicting memory was not imported.");
                    continue;
                }

                var conflictKey = $"{candidateId:N}:{memoryId:N}";
                var existing = createdConflictKeys.Contains(conflictKey)
                    ? null
                    : await memoryStore.GetPendingMemoryConflictAsync(candidateId, memoryId, cancellationToken);
                if (existing is not null || createdConflictKeys.Contains(conflictKey))
                {
                    if (!skipDuplicates)
                    {
                        conflictCounts.Failed++;
                        AddIssue(issues, "conflict", item.CandidateFingerprint, "A pending conflict for this pair already exists.");
                        continue;
                    }

                    conflictCounts.Skipped++;
                    continue;
                }

                var conversationId = await ResolveConversationAsync(
                    ownerId,
                    item.ConversationId,
                    conversationsById,
                    conversationMap,
                    conversationCounts,
                    dryRun,
                    cancellationToken);
                createdConflictKeys.Add(conflictKey);
                if (!dryRun)
                {
                    var conflict = new MemoryConflict(
                        ownerId,
                        conversationId,
                        candidateId,
                        memoryId,
                        item.Confidence,
                        item.Reason);
                    memoryStore.AddMemoryConflict(conflict);
                    memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForConflict(
                        MemoryAuditAction.ConflictOpened,
                        conflict,
                        MemoryAuditActorKind.User,
                        "Imported from memory profile backup."));
                }

                conflictCounts.Created++;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                conflictCounts.Failed++;
                AddIssue(issues, "conflict", item.CandidateFingerprint, exception.Message);
            }
        }

        if (!dryRun)
        {
            await memoryStore.SaveChangesAsync(cancellationToken);
        }

        return new MemoryImportResult(
            dryRun,
            skipDuplicates,
            pinExplicit,
            ownerId,
            string.IsNullOrWhiteSpace(document.OwnerId) ? null : document.OwnerId.Trim(),
            conversationCounts.ToCounts(),
            memoryCounts.ToCounts(),
            candidateCounts.ToCounts(),
            conflictCounts.ToCounts(),
            issues);
    }

    private async Task<Guid> ResolveConversationAsync(
        string ownerId,
        Guid sourceConversationId,
        IReadOnlyDictionary<Guid, MemoryExportConversation> conversationsById,
        Dictionary<Guid, Guid> conversationMap,
        Counter counts,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        if (conversationMap.TryGetValue(sourceConversationId, out var mappedId))
        {
            return mappedId;
        }

        conversationsById.TryGetValue(sourceConversationId, out var exported);
        var externalId = string.IsNullOrWhiteSpace(exported?.ExternalId)
            ? $"imported-{sourceConversationId:N}"
            : exported.ExternalId.Trim();
        var existing = await memoryStore.GetConversationByExternalIdAsync(ownerId, externalId, cancellationToken);
        if (existing is not null)
        {
            existing.TryUpdateTitle(exported?.Title);
            conversationMap[sourceConversationId] = existing.Id;
            counts.Skipped++;
            return existing.Id;
        }

        counts.Created++;
        if (dryRun)
        {
            var placeholder = Guid.NewGuid();
            conversationMap[sourceConversationId] = placeholder;
            return placeholder;
        }

        var conversation = new Conversation(ownerId, externalId, exported?.Title);
        memoryStore.AddConversation(conversation);
        conversationMap[sourceConversationId] = conversation.Id;
        return conversation.Id;
    }

    private void TrySetEmbedding(MemoryEntity memory, float[]? embedding)
    {
        if (embedding is null || embedding.Length == 0)
        {
            return;
        }

        if (embedding.Length != memoryAiOptions.Value.EmbeddingDimensions)
        {
            return;
        }

        memory.SetEmbedding(embedding);
    }

    private async Task<IReadOnlyList<Conversation>> LoadReferencedConversationsAsync(
        string ownerId,
        IReadOnlyList<MemoryEntity> memories,
        IReadOnlyList<MemoryCandidate> candidates,
        IReadOnlyList<MemoryConflict> conflicts,
        CancellationToken cancellationToken)
    {
        var ids = memories.Select(memory => memory.ConversationId)
            .Concat(candidates.Select(candidate => candidate.ConversationId))
            .Concat(conflicts.Select(conflict => conflict.ConversationId))
            .Distinct()
            .ToArray();
        var conversations = await memoryStore.GetConversationsForOwnerAsync(ownerId, cancellationToken);
        var byId = conversations.ToDictionary(conversation => conversation.Id);
        foreach (var id in ids)
        {
            if (byId.ContainsKey(id))
            {
                continue;
            }

            var conversation = await memoryStore.GetConversationAsync(id, cancellationToken);
            if (conversation is not null)
            {
                byId[id] = conversation;
            }
        }

        return ids
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .ToArray();
    }

    private static MemoryExportMemory ToExportMemory(MemoryEntity memory)
    {
        return new MemoryExportMemory(
            memory.Id,
            memory.ConversationId,
            memory.Scope,
            memory.Content,
            memory.Type,
            memory.Origin,
            memory.IsPinned,
            memory.Importance,
            memory.Confidence,
            memory.Fingerprint,
            memory.ValidFrom,
            memory.ValidUntil,
            memory.SourceSummary,
            memory.SourceMetadataJson,
            memory.Embedding,
            memory.CreatedAt,
            memory.UpdatedAt);
    }

    private static MemoryExportCandidate ToExportCandidate(MemoryCandidate candidate)
    {
        return new MemoryExportCandidate(
            candidate.Id,
            candidate.ConversationId,
            candidate.Scope,
            candidate.Content,
            candidate.Type,
            candidate.Importance,
            candidate.Confidence,
            candidate.Fingerprint,
            candidate.EvidenceCount,
            candidate.LastEvidenceAt,
            candidate.ValidFrom,
            candidate.ValidUntil,
            candidate.CreatedAt,
            candidate.UpdatedAt);
    }

    private static MemoryExportConflict ToExportConflict(MemoryConflict conflict)
    {
        return new MemoryExportConflict(
            conflict.Id,
            conflict.ConversationId,
            conflict.CandidateId,
            conflict.ConflictingMemoryId,
            conflict.Candidate?.Fingerprint ?? string.Empty,
            conflict.ConflictingMemory?.Fingerprint ?? string.Empty,
            conflict.Confidence,
            conflict.Reason);
    }

    private static MemoryExportAuditMetadata ToAuditMetadata(IReadOnlyList<MemoryAuditLog> entries)
    {
        var byAction = entries
            .GroupBy(entry => entry.Action)
            .OrderBy(group => group.Key)
            .Select(group => new MemoryExportAuditActionCount(group.Key, group.Count()))
            .ToArray();

        return new MemoryExportAuditMetadata(
            entries.Count,
            byAction,
            entries.Count == 0 ? null : entries[0].OccurredAt,
            entries.Take(MaxRecentAudit).Select(MemoryAuditService.ToResponse).ToArray());
    }

    private static void RestoreEvidenceCount(MemoryCandidate candidate, int evidenceCount)
    {
        var count = Math.Clamp(evidenceCount, 0, MaxRestoredEvidenceCount);
        for (var index = 0; index < count; index++)
        {
            candidate.AddEvidence(candidate.Importance, candidate.Confidence);
        }
    }

    private static bool TryResolveImportedId(
        Guid sourceId,
        IReadOnlyDictionary<Guid, Guid> map,
        out Guid targetId)
    {
        return map.TryGetValue(sourceId, out targetId);
    }

    private static void AddIssue(
        List<MemoryImportIssue> issues,
        string kind,
        string? fingerprint,
        string reason)
    {
        if (issues.Count >= MaxImportIssues)
        {
            return;
        }

        issues.Add(new MemoryImportIssue(kind, fingerprint, reason));
    }

    private static string RequireOwnerId(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(ownerId));
        }

        return ownerId.Trim();
    }

    private sealed class Counter
    {
        public int Created { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }

        public MemoryImportCounts ToCounts() => new(Created, Skipped, Failed);
    }
}
