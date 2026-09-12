namespace Memory.Application.Tests.Fakes;

using System.Reflection;
using Memory.Application.Abstractions.Persistence;
using Memory.Application.Owners;
using Memory.Domain.Conversations;
using Memory.Domain.Ingestion;
using Memory.Domain.Memories;
using Memory.Domain.Tools;
using MemoryEntity = Memory.Domain.Memories.Memory;

internal sealed class FakeMemoryStore : IMemoryStore
{
    private readonly Dictionary<Guid, Conversation> _conversations = new();
    private readonly Dictionary<Guid, Message> _messages = new();
    private readonly Dictionary<Guid, MemoryEntity> _memories = new();
    private readonly Dictionary<Guid, MemoryCandidate> _candidates = new();
    private readonly Dictionary<Guid, MemoryEvidence> _evidence = new();
    private readonly Dictionary<Guid, MemoryConflict> _conflicts = new();
    private readonly Dictionary<Guid, MemoryIngestionJob> _jobs = new();
    private readonly List<MemoryAuditLog> _auditLogs = new();
    private readonly List<ToolAuditLog> _toolAuditLogs = new();

    public bool DatabaseCanConnect { get; set; } = true;
    public string? LatestMigration { get; set; } = "test";

    public IReadOnlyCollection<Conversation> Conversations => _conversations.Values;
    public IReadOnlyCollection<MemoryEntity> Memories => _memories.Values;
    public IReadOnlyCollection<MemoryCandidate> Candidates => _candidates.Values;
    public IReadOnlyCollection<MemoryEvidence> Evidence => _evidence.Values;
    public IReadOnlyCollection<MemoryConflict> Conflicts => _conflicts.Values;
    public IReadOnlyCollection<MemoryIngestionJob> Jobs => _jobs.Values;
    public IReadOnlyList<MemoryAuditLog> AuditLogs => _auditLogs;
    public IReadOnlyList<ToolAuditLog> ToolAuditLogs => _toolAuditLogs;

    public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        return operation();
    }

    public Task<bool> ConversationExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_conversations.ContainsKey(id));
    }

    public Task<Conversation?> GetConversationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _conversations.TryGetValue(id, out var conversation);
        return Task.FromResult(conversation);
    }

    public Task<Conversation?> GetConversationForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return GetConversationAsync(id, cancellationToken);
    }

    public Task<Conversation?> GetConversationByExternalIdAsync(
        string ownerId,
        string externalId,
        CancellationToken cancellationToken = default)
    {
        var conversation = _conversations.Values.FirstOrDefault(item =>
            item.OwnerId == ownerId.Trim() && item.ExternalId == externalId.Trim());
        return Task.FromResult(conversation);
    }

    public Task<IReadOnlyList<Conversation>> GetConversationsForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = ownerId.Trim();
        var conversations = _conversations.Values
            .Where(conversation => conversation.OwnerId == normalizedOwnerId)
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .ThenByDescending(conversation => conversation.CreatedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyList<Conversation>>(conversations);
    }

    public Task<IReadOnlyList<OwnerSummary>> ListOwnersAsync(CancellationToken cancellationToken = default)
    {
        var conversations = _conversations.Values
            .GroupBy(conversation => conversation.OwnerId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (Count: group.Count(), Last: group.Max(item => item.UpdatedAt)),
                StringComparer.Ordinal);
        var memories = _memories.Values
            .Where(memory => memory.Status == MemoryStatus.Active)
            .GroupBy(memory => memory.OwnerId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (Count: group.Count(), Last: group.Max(item => item.UpdatedAt)),
                StringComparer.Ordinal);

        var owners = conversations.Keys
            .Concat(memories.Keys)
            .Distinct(StringComparer.Ordinal)
            .Select(ownerId =>
            {
                conversations.TryGetValue(ownerId, out var conversation);
                memories.TryGetValue(ownerId, out var memory);
                DateTimeOffset? last = conversations.ContainsKey(ownerId) ? conversation.Last : null;
                if (memories.ContainsKey(ownerId) && (last is null || memory.Last > last))
                {
                    last = memory.Last;
                }

                return new OwnerSummary(ownerId, conversation.Count, memory.Count, last);
            })
            .OrderByDescending(owner => owner.LastActivityAt)
            .ThenBy(owner => owner.OwnerId, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyList<OwnerSummary>>(owners);
    }

    public Task<Message?> GetMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _messages.TryGetValue(id, out var message);
        return Task.FromResult(message);
    }

    public Task<IReadOnlyList<Message>> GetRecentMessagesAsync(
        Guid conversationId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var messages = _messages.Values
            .Where(message => message.ConversationId == conversationId)
            .OrderByDescending(message => message.SequenceNumber)
            .Take(take)
            .OrderBy(message => message.SequenceNumber)
            .ToArray();

        return Task.FromResult<IReadOnlyList<Message>>(messages);
    }

    public Task<IReadOnlyList<MemoryIngestionJob>> GetIngestionJobsForConversationAsync(
        Guid conversationId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var jobs = _jobs.Values
            .Where(job => job.ConversationId == conversationId)
            .OrderByDescending(job => job.CreatedAt)
            .Take(NormalizeJobLimit(take))
            .ToArray();

        return Task.FromResult<IReadOnlyList<MemoryIngestionJob>>(jobs);
    }

    public Task<IReadOnlyList<MemoryIngestionJob>> GetIngestionJobsForOwnerAsync(
        string ownerId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = ownerId.Trim();
        var conversationIds = _conversations.Values
            .Where(conversation => conversation.OwnerId == normalizedOwnerId)
            .Select(conversation => conversation.Id)
            .ToHashSet();

        var jobs = _jobs.Values
            .Where(job => conversationIds.Contains(job.ConversationId))
            .OrderByDescending(job => job.CreatedAt)
            .Take(NormalizeJobLimit(take))
            .ToArray();

        return Task.FromResult<IReadOnlyList<MemoryIngestionJob>>(jobs);
    }

    public Task<MemoryEntity?> GetMemoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _memories.TryGetValue(id, out var memory);
        return Task.FromResult(memory);
    }

    public Task<MemoryEntity?> GetActiveMemoryByFingerprintAsync(
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var memory = _memories.Values.FirstOrDefault(item =>
            item.Fingerprint == fingerprint && item.IsEffective(now));
        return Task.FromResult(memory);
    }

    public Task<MemoryCandidate?> GetPendingMemoryCandidateByFingerprintAsync(
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        var candidate = _candidates.Values.FirstOrDefault(item =>
            item.Fingerprint == fingerprint && item.Status == MemoryCandidateStatus.Pending);
        return Task.FromResult(candidate);
    }

    public Task<MemoryCandidate?> GetMemoryCandidateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _candidates.TryGetValue(id, out var candidate);
        return Task.FromResult(candidate);
    }

    public Task<IReadOnlyList<MemoryCandidate>> GetPendingMemoryCandidatesForConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var candidates = _candidates.Values
            .Where(candidate => candidate.ConversationId == conversationId)
            .Where(candidate => candidate.Status == MemoryCandidateStatus.Pending)
            .OrderByDescending(candidate => candidate.Importance)
            .ThenByDescending(candidate => candidate.Confidence)
            .ThenByDescending(candidate => candidate.EvidenceCount)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MemoryCandidate>>(candidates);
    }

    public Task<IReadOnlyList<MemoryCandidate>> GetPendingMemoryCandidatesForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = ownerId.Trim();
        var candidates = _candidates.Values
            .Where(candidate => candidate.OwnerId == normalizedOwnerId)
            .Where(candidate => candidate.Status == MemoryCandidateStatus.Pending)
            .OrderByDescending(candidate => candidate.Importance)
            .ThenByDescending(candidate => candidate.Confidence)
            .ThenByDescending(candidate => candidate.EvidenceCount)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MemoryCandidate>>(candidates);
    }

    public Task<IReadOnlyList<MemoryEvidence>> GetMemoryEvidenceAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default)
    {
        var evidence = _evidence.Values
            .Where(item => item.CandidateId == candidateId)
            .OrderByDescending(item => item.CreatedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MemoryEvidence>>(evidence);
    }

    public Task<bool> MemoryEvidenceExistsAsync(
        Guid candidateId,
        Guid sourceMessageId,
        CancellationToken cancellationToken = default)
    {
        var exists = _evidence.Values.Any(item =>
            item.CandidateId == candidateId && item.SourceMessageId == sourceMessageId);
        return Task.FromResult(exists);
    }

    public Task<IReadOnlyList<MemoryConflict>> GetPendingMemoryConflictsForConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var conflicts = PendingConflicts()
            .Where(conflict => conflict.ConversationId == conversationId)
            .Select(HydrateConflict)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MemoryConflict>>(conflicts);
    }

    public Task<IReadOnlyList<MemoryConflict>> GetPendingMemoryConflictsForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = ownerId.Trim();
        var conflicts = PendingConflicts()
            .Where(conflict => conflict.OwnerId == normalizedOwnerId)
            .Select(HydrateConflict)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MemoryConflict>>(conflicts);
    }

    public Task<MemoryConflict?> GetMemoryConflictAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _conflicts.TryGetValue(id, out var conflict);
        return Task.FromResult(conflict is null ? null : HydrateConflict(conflict));
    }

    public Task<MemoryConflict?> GetPendingMemoryConflictAsync(
        Guid candidateId,
        Guid conflictingMemoryId,
        CancellationToken cancellationToken = default)
    {
        var conflict = PendingConflicts().FirstOrDefault(item =>
            item.CandidateId == candidateId && item.ConflictingMemoryId == conflictingMemoryId);
        return Task.FromResult(conflict is null ? null : HydrateConflict(conflict));
    }

    public Task<IReadOnlyList<MemoryConflict>> GetPendingMemoryConflictsForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default)
    {
        var conflicts = PendingConflicts()
            .Where(conflict => conflict.CandidateId == candidateId)
            .Select(HydrateConflict)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MemoryConflict>>(conflicts);
    }

    public Task<IReadOnlyList<MemoryEntity>> GetActiveMemoriesAsync(
        MemoryLookup lookup,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var memories = ApplyLookup(_memories.Values, lookup)
            .Where(memory => memory.IsEffective(now))
            .OrderByDescending(memory => memory.Importance)
            .ThenByDescending(memory => memory.Confidence)
            .ThenByDescending(memory => memory.CreatedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MemoryEntity>>(memories);
    }

    public Task<IReadOnlyList<MemoryEntity>> GetActiveMemoriesForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = ownerId.Trim();
        var now = DateTimeOffset.UtcNow;
        var memories = _memories.Values
            .Where(memory => memory.OwnerId == normalizedOwnerId)
            .Where(memory => memory.IsEffective(now))
            .OrderByDescending(memory => memory.Importance)
            .ThenByDescending(memory => memory.Confidence)
            .ThenByDescending(memory => memory.CreatedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MemoryEntity>>(memories);
    }

    public Task<IReadOnlyList<MemoryEntity>> GetMemoriesByIdsForOwnerAsync(
        string ownerId,
        IReadOnlyCollection<Guid> memoryIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = ownerId.Trim();
        var ids = memoryIds.ToHashSet();
        var memories = _memories.Values
            .Where(memory => memory.OwnerId == normalizedOwnerId)
            .Where(memory => ids.Contains(memory.Id))
            .ToArray();

        return Task.FromResult<IReadOnlyList<MemoryEntity>>(memories);
    }

    public Task<IReadOnlyList<MemorySimilarity>> SearchActiveMemoriesAsync(
        MemoryLookup lookup,
        IReadOnlyList<float> embedding,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var matches = ApplyLookup(_memories.Values, lookup)
            .Where(memory => memory.IsEffective(now))
            .Where(memory => memory.Embedding is not null)
            .Select(memory => new MemorySimilarity(memory, CosineDistance(embedding, memory.Embedding!)))
            .OrderBy(match => match.Distance)
            .Take(Math.Max(1, limit))
            .ToArray();

        return Task.FromResult<IReadOnlyList<MemorySimilarity>>(matches);
    }

    public void AddConversation(Conversation conversation)
    {
        _conversations[conversation.Id] = conversation;
    }

    public void AddMessage(Message message)
    {
        _messages[message.Id] = message;
    }

    public void AddMemory(MemoryEntity memory)
    {
        _memories[memory.Id] = memory;
    }

    public void AddMemoryCandidate(MemoryCandidate candidate)
    {
        _candidates[candidate.Id] = candidate;
    }

    public void AddMemoryEvidence(MemoryEvidence evidence)
    {
        _evidence[evidence.Id] = evidence;
    }

    public void AddMemoryConflict(MemoryConflict conflict)
    {
        _conflicts[conflict.Id] = conflict;
    }

    public void AddIngestionJob(MemoryIngestionJob job)
    {
        _jobs[job.Id] = job;
    }

    public void AddMemoryAuditLog(MemoryAuditLog entry)
    {
        _auditLogs.Add(entry);
    }

    public void AddToolAuditLog(ToolAuditLog entry)
    {
        _toolAuditLogs.Add(entry);
    }

    public Task<IReadOnlyList<MemoryAuditLog>> GetMemoryAuditLogsAsync(
        string ownerId,
        Guid? memoryId,
        Guid? conversationId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = ownerId.Trim();
        var limit = take <= 0 ? 50 : Math.Min(take, 200);
        var entries = _auditLogs
            .Where(entry => entry.OwnerId == normalizedOwnerId)
            .Where(entry => memoryId is null || entry.MemoryId == memoryId)
            .Where(entry => conversationId is null || entry.ConversationId == conversationId)
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Take(limit)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MemoryAuditLog>>(entries);
    }

    public Task<IReadOnlyList<ToolAuditLog>> GetToolAuditLogsAsync(
        string ownerId,
        Guid? conversationId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = ownerId.Trim();
        var limit = take <= 0 ? 50 : Math.Min(take, 200);
        var entries = _toolAuditLogs
            .Where(entry => entry.OwnerId == normalizedOwnerId)
            .Where(entry => conversationId is null || entry.ConversationId == conversationId)
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Take(limit)
            .ToArray();

        return Task.FromResult<IReadOnlyList<ToolAuditLog>>(entries);
    }

    public Task<IReadOnlyList<MemoryEntity>> GetExpiredActiveMemoriesAsync(
        DateTimeOffset now,
        int take,
        CancellationToken cancellationToken = default)
    {
        var limit = NormalizeJobLimit(take);
        var memories = _memories.Values
            .Where(memory => memory.Status == MemoryStatus.Active)
            .Where(memory => memory.ValidUntil is not null && memory.ValidUntil <= now)
            .OrderBy(memory => memory.ValidUntil)
            .ThenBy(memory => memory.CreatedAt)
            .Take(limit)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MemoryEntity>>(memories);
    }

    public Task<DatabaseRuntimeInfo> GetDatabaseRuntimeAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DatabaseRuntimeInfo(DatabaseCanConnect, DatabaseCanConnect ? LatestMigration : null));
    }

    public Task<RuntimeQueueCounts> GetRuntimeQueueCountsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new RuntimeQueueCounts(
            _jobs.Values.Count(job => job.Status == IngestionJobStatus.Pending),
            _jobs.Values.Count(job => job.Status == IngestionJobStatus.Processing),
            _candidates.Values.Count(candidate => candidate.Status == MemoryCandidateStatus.Pending),
            _conflicts.Values.Count(conflict => conflict.Status == MemoryConflictStatus.Pending)));
    }

    public Task<IReadOnlyList<MemoryCandidate>> GetPendingCandidatesForRetentionAsync(
        DateTimeOffset now,
        DateTimeOffset? staleBefore,
        decimal maxConfidence,
        decimal maxImportance,
        int take,
        CancellationToken cancellationToken = default)
    {
        var limit = NormalizeJobLimit(take);
        var candidates = _candidates.Values
            .Where(candidate => candidate.Status == MemoryCandidateStatus.Pending)
            .Where(candidate =>
            {
                var expired = candidate.ValidUntil is not null && candidate.ValidUntil <= now;
                if (staleBefore is not DateTimeOffset cutoff)
                {
                    return expired;
                }

                var lastActivity = candidate.LastEvidenceAt ?? candidate.UpdatedAt;
                var weakAndStale = lastActivity < cutoff
                    && candidate.Confidence < maxConfidence
                    && candidate.Importance < maxImportance;

                return expired || weakAndStale;
            })
            .OrderBy(candidate => candidate.UpdatedAt)
            .ThenBy(candidate => candidate.CreatedAt)
            .Take(limit)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MemoryCandidate>>(candidates);
    }

    public Task<int> RemoveCompletedIngestionJobsAsync(
        DateTimeOffset cutoff,
        int take,
        CancellationToken cancellationToken = default)
    {
        var limit = NormalizeJobLimit(take);
        var jobs = _jobs.Values
            .Where(job =>
                (job.Status == IngestionJobStatus.Succeeded || job.Status == IngestionJobStatus.Skipped)
                && job.UpdatedAt < cutoff)
            .OrderBy(job => job.UpdatedAt)
            .ThenBy(job => job.CreatedAt)
            .Take(limit)
            .ToArray();

        foreach (var job in jobs)
        {
            _jobs.Remove(job.Id);
        }

        return Task.FromResult(jobs.Length);
    }

    public Task<MemoryIngestionJob?> ClaimNextIngestionJobAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var job = _jobs.Values
            .Where(item =>
                item.Status == IngestionJobStatus.Pending
                || (item.Status == IngestionJobStatus.Processing && item.LockedUntil is not null && item.LockedUntil < now))
            .OrderBy(item => item.CreatedAt)
            .FirstOrDefault();

        job?.MarkProcessing(now);
        return Task.FromResult(job);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    private IEnumerable<MemoryConflict> PendingConflicts()
    {
        return _conflicts.Values
            .Where(conflict => conflict.Status == MemoryConflictStatus.Pending)
            .OrderByDescending(conflict => conflict.Confidence)
            .ThenByDescending(conflict => conflict.CreatedAt);
    }

    private MemoryConflict HydrateConflict(MemoryConflict conflict)
    {
        if (_candidates.TryGetValue(conflict.CandidateId, out var candidate))
        {
            SetNavigation(conflict, nameof(MemoryConflict.Candidate), candidate);
        }

        if (_memories.TryGetValue(conflict.ConflictingMemoryId, out var memory))
        {
            SetNavigation(conflict, nameof(MemoryConflict.ConflictingMemory), memory);
        }

        return conflict;
    }

    private static void SetNavigation(MemoryConflict conflict, string propertyName, object value)
    {
        typeof(MemoryConflict)
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
            ?.SetValue(conflict, value);
    }

    private static IEnumerable<MemoryEntity> ApplyLookup(IEnumerable<MemoryEntity> memories, MemoryLookup lookup)
    {
        var ownerId = lookup.OwnerId.Trim();
        var includeConversation = lookup.IncludeConversationScope && lookup.ConversationId is not null;
        var includeUser = lookup.IncludeUserScope;

        return memories
            .Where(memory => memory.OwnerId == ownerId)
            .Where(memory =>
                (includeConversation && memory.Scope == MemoryScope.Conversation && memory.ConversationId == lookup.ConversationId)
                || (includeUser && memory.Scope == MemoryScope.User));
    }

    private static double CosineDistance(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        if (left.Count != right.Count)
        {
            return 2d;
        }

        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;

        for (var index = 0; index < left.Count; index++)
        {
            dot += left[index] * right[index];
            leftNorm += left[index] * left[index];
            rightNorm += right[index] * right[index];
        }

        var denominator = Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm);
        if (denominator == 0)
        {
            return 1d;
        }

        return 1d - (dot / denominator);
    }

    private static int NormalizeJobLimit(int take)
    {
        return take <= 0 ? 50 : Math.Min(take, 200);
    }
}
