namespace Memory.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Memory.Application.Abstractions.Persistence;
using Memory.Application.Owners;
using Memory.Domain.Conversations;
using Memory.Domain.Ingestion;
using Memory.Domain.Memories;
using Memory.Domain.Tools;
using Pgvector;
using MemoryEntity = Memory.Domain.Memories.Memory;

internal sealed class MemoryStore(MemoryDbContext dbContext) : IMemoryStore
{
    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var result = await operation();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public Task<bool> ConversationExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Conversations.AnyAsync(conversation => conversation.Id == id, cancellationToken);
    }

    public Task<Conversation?> GetConversationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Conversations.FirstOrDefaultAsync(
            conversation => conversation.Id == id,
            cancellationToken);
    }

    public async Task<Conversation?> GetConversationForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM conversations WHERE id = {id} FOR UPDATE",
            cancellationToken);

        return await dbContext.Conversations.FirstOrDefaultAsync(
            conversation => conversation.Id == id,
            cancellationToken);
    }

    public Task<Conversation?> GetConversationByExternalIdAsync(
        string ownerId,
        string externalId,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = ownerId.Trim();
        var normalizedExternalId = externalId.Trim();

        return dbContext.Conversations.FirstOrDefaultAsync(
            conversation => conversation.OwnerId == normalizedOwnerId
                && conversation.ExternalId == normalizedExternalId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Conversation>> GetConversationsForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = ownerId.Trim();

        return await dbContext.Conversations
            .Where(conversation => conversation.OwnerId == normalizedOwnerId)
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .ThenByDescending(conversation => conversation.CreatedAt)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OwnerSummary>> ListOwnersAsync(CancellationToken cancellationToken = default)
    {
        var conversations = await dbContext.Conversations
            .AsNoTracking()
            .GroupBy(conversation => conversation.OwnerId)
            .Select(group => new
            {
                OwnerId = group.Key,
                Count = group.Count(),
                Last = group.Max(item => item.UpdatedAt)
            })
            .ToArrayAsync(cancellationToken);
        var memories = await dbContext.Memories
            .AsNoTracking()
            .Where(memory => memory.Status == MemoryStatus.Active)
            .GroupBy(memory => memory.OwnerId)
            .Select(group => new
            {
                OwnerId = group.Key,
                Count = group.Count(),
                Last = group.Max(item => item.UpdatedAt)
            })
            .ToArrayAsync(cancellationToken);

        return conversations
            .Select(row => row.OwnerId)
            .Concat(memories.Select(row => row.OwnerId))
            .Distinct(StringComparer.Ordinal)
            .Select(ownerId =>
            {
                var conversation = conversations.FirstOrDefault(row => row.OwnerId == ownerId);
                var memory = memories.FirstOrDefault(row => row.OwnerId == ownerId);
                DateTimeOffset? last = conversation?.Last;
                if (memory is not null && (last is null || memory.Last > last))
                {
                    last = memory.Last;
                }

                return new OwnerSummary(
                    ownerId,
                    conversation?.Count ?? 0,
                    memory?.Count ?? 0,
                    last);
            })
            .OrderByDescending(owner => owner.LastActivityAt)
            .ThenBy(owner => owner.OwnerId, StringComparer.Ordinal)
            .ToArray();
    }

    public Task<Message?> GetMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Messages.FirstOrDefaultAsync(message => message.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> GetRecentMessagesAsync(
        Guid conversationId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var messages = await dbContext.Messages
            .Where(message => message.ConversationId == conversationId)
            .OrderByDescending(message => message.SequenceNumber)
            .Take(take)
            .ToArrayAsync(cancellationToken);

        return messages
            .OrderBy(message => message.SequenceNumber)
            .ToArray();
    }

    public async Task<IReadOnlyList<MemoryIngestionJob>> GetIngestionJobsForConversationAsync(
        Guid conversationId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var limit = NormalizeJobLimit(take);

        return await dbContext.IngestionJobs
            .AsNoTracking()
            .Where(job => job.ConversationId == conversationId)
            .OrderByDescending(job => job.CreatedAt)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryIngestionJob>> GetIngestionJobsForOwnerAsync(
        string ownerId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = ownerId.Trim();
        var limit = NormalizeJobLimit(take);

        return await dbContext.IngestionJobs
            .AsNoTracking()
            .Join(
                dbContext.Conversations.AsNoTracking().Where(conversation => conversation.OwnerId == normalizedOwnerId),
                job => job.ConversationId,
                conversation => conversation.Id,
                (job, _) => job)
            .OrderByDescending(job => job.CreatedAt)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
    }

    public Task<MemoryEntity?> GetMemoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Memories.FirstOrDefaultAsync(memory => memory.Id == id, cancellationToken);
    }

    public async Task<MemoryEntity?> GetActiveMemoryByFingerprintAsync(
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return await ApplyActiveFilter(dbContext.Memories.Where(memory => memory.Fingerprint == fingerprint), now)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<MemoryCandidate?> GetPendingMemoryCandidateByFingerprintAsync(
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.MemoryCandidates
            .FirstOrDefaultAsync(
                candidate => candidate.Fingerprint == fingerprint
                    && candidate.Status == MemoryCandidateStatus.Pending,
                cancellationToken);
    }

    public Task<MemoryCandidate?> GetMemoryCandidateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.MemoryCandidates.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryCandidate>> GetPendingMemoryCandidatesForConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.MemoryCandidates
            .Where(candidate => candidate.ConversationId == conversationId)
            .Where(candidate => candidate.Status == MemoryCandidateStatus.Pending)
            .OrderByDescending(candidate => candidate.Importance)
            .ThenByDescending(candidate => candidate.Confidence)
            .ThenByDescending(candidate => candidate.EvidenceCount)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryCandidate>> GetPendingMemoryCandidatesForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = ownerId.Trim();

        return await dbContext.MemoryCandidates
            .Where(candidate => candidate.OwnerId == normalizedOwnerId)
            .Where(candidate => candidate.Status == MemoryCandidateStatus.Pending)
            .OrderByDescending(candidate => candidate.Importance)
            .ThenByDescending(candidate => candidate.Confidence)
            .ThenByDescending(candidate => candidate.EvidenceCount)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryEvidence>> GetMemoryEvidenceAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.MemoryEvidence
            .Where(evidence => evidence.CandidateId == candidateId)
            .OrderByDescending(evidence => evidence.CreatedAt)
            .ToArrayAsync(cancellationToken);
    }

    public Task<bool> MemoryEvidenceExistsAsync(
        Guid candidateId,
        Guid sourceMessageId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.MemoryEvidence.AnyAsync(
            evidence => evidence.CandidateId == candidateId
                && evidence.SourceMessageId == sourceMessageId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryConflict>> GetPendingMemoryConflictsForConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.MemoryConflicts
            .Where(conflict => conflict.ConversationId == conversationId)
            .Where(conflict => conflict.Status == MemoryConflictStatus.Pending)
            .Include(conflict => conflict.Candidate)
            .Include(conflict => conflict.ConflictingMemory)
            .OrderByDescending(conflict => conflict.Confidence)
            .ThenByDescending(conflict => conflict.CreatedAt)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryConflict>> GetPendingMemoryConflictsForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = ownerId.Trim();

        return await dbContext.MemoryConflicts
            .Where(conflict => conflict.OwnerId == normalizedOwnerId)
            .Where(conflict => conflict.Status == MemoryConflictStatus.Pending)
            .Include(conflict => conflict.Candidate)
            .Include(conflict => conflict.ConflictingMemory)
            .OrderByDescending(conflict => conflict.Confidence)
            .ThenByDescending(conflict => conflict.CreatedAt)
            .ToArrayAsync(cancellationToken);
    }

    public Task<MemoryConflict?> GetMemoryConflictAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.MemoryConflicts
            .Include(conflict => conflict.Candidate)
            .Include(conflict => conflict.ConflictingMemory)
            .FirstOrDefaultAsync(conflict => conflict.Id == id, cancellationToken);
    }

    public Task<MemoryConflict?> GetPendingMemoryConflictAsync(
        Guid candidateId,
        Guid conflictingMemoryId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.MemoryConflicts.FirstOrDefaultAsync(
            conflict => conflict.CandidateId == candidateId
                && conflict.ConflictingMemoryId == conflictingMemoryId
                && conflict.Status == MemoryConflictStatus.Pending,
            cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryConflict>> GetPendingMemoryConflictsForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.MemoryConflicts
            .Where(conflict => conflict.CandidateId == candidateId)
            .Where(conflict => conflict.Status == MemoryConflictStatus.Pending)
            .Include(conflict => conflict.Candidate)
            .Include(conflict => conflict.ConflictingMemory)
            .OrderByDescending(conflict => conflict.Confidence)
            .ThenByDescending(conflict => conflict.CreatedAt)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryEntity>> GetActiveMemoriesAsync(
        MemoryLookup lookup,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return await ApplyActiveFilter(ApplyLookup(dbContext.Memories, lookup), now)
            .OrderByDescending(memory => memory.Importance)
            .ThenByDescending(memory => memory.Confidence)
            .ThenByDescending(memory => memory.CreatedAt)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryEntity>> GetActiveMemoriesForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = ownerId.Trim();
        var now = DateTimeOffset.UtcNow;

        return await ApplyActiveFilter(dbContext.Memories.Where(memory => memory.OwnerId == normalizedOwnerId), now)
            .OrderByDescending(memory => memory.Importance)
            .ThenByDescending(memory => memory.Confidence)
            .ThenByDescending(memory => memory.CreatedAt)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryEntity>> GetMemoriesByIdsForOwnerAsync(
        string ownerId,
        IReadOnlyCollection<Guid> memoryIds,
        CancellationToken cancellationToken = default)
    {
        if (memoryIds.Count == 0)
        {
            return [];
        }

        var normalizedOwnerId = ownerId.Trim();

        return await dbContext.Memories
            .Where(memory => memory.OwnerId == normalizedOwnerId)
            .Where(memory => memoryIds.Contains(memory.Id))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemorySimilarity>> SearchActiveMemoriesAsync(
        MemoryLookup lookup,
        IReadOnlyList<float> embedding,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var vector = new Vector(embedding as float[] ?? embedding.ToArray());
        var take = Math.Max(1, limit);
        var ownerId = lookup.OwnerId.Trim();
        var conversationId = lookup.ConversationId ?? Guid.Empty;
        var includeUser = lookup.IncludeUserScope;
        var includeConversation = lookup.IncludeConversationScope && lookup.ConversationId is not null;
        var conversationScope = nameof(MemoryScope.Conversation);
        var userScope = nameof(MemoryScope.User);
        var activeStatus = nameof(MemoryStatus.Active);

        var rows = await dbContext.Database
            .SqlQuery<MemoryDistanceRow>($"""
                SELECT id AS "Id", (embedding <=> {vector}) AS "Distance"
                FROM memories
                WHERE owner_id = {ownerId}
                  AND status = {activeStatus}
                  AND embedding IS NOT NULL
                  AND (valid_from IS NULL OR valid_from <= {now})
                  AND (valid_until IS NULL OR valid_until > {now})
                  AND (
                        ({includeConversation} AND scope = {conversationScope} AND conversation_id = {conversationId})
                     OR ({includeUser} AND scope = {userScope})
                  )
                ORDER BY embedding <=> {vector}
                LIMIT {take}
                """)
            .ToArrayAsync(cancellationToken);

        if (rows.Length == 0)
        {
            return [];
        }

        var ids = rows.Select(row => row.Id).ToArray();
        var memories = await dbContext.Memories
            .Where(memory => ids.Contains(memory.Id))
            .ToArrayAsync(cancellationToken);
        var memoriesById = memories.ToDictionary(memory => memory.Id);

        return rows
            .Where(row => memoriesById.ContainsKey(row.Id))
            .Select(row => new MemorySimilarity(memoriesById[row.Id], row.Distance))
            .ToArray();
    }

    public void AddConversation(Conversation conversation)
    {
        dbContext.Conversations.Add(conversation);
    }

    public void AddMessage(Message message)
    {
        dbContext.Messages.Add(message);
    }

    public void AddMemory(MemoryEntity memory)
    {
        dbContext.Memories.Add(memory);
    }

    public void AddMemoryCandidate(MemoryCandidate candidate)
    {
        dbContext.MemoryCandidates.Add(candidate);
    }

    public void AddMemoryEvidence(MemoryEvidence evidence)
    {
        dbContext.MemoryEvidence.Add(evidence);
    }

    public void AddMemoryConflict(MemoryConflict conflict)
    {
        dbContext.MemoryConflicts.Add(conflict);
    }

    public void AddIngestionJob(MemoryIngestionJob job)
    {
        dbContext.IngestionJobs.Add(job);
    }

    public void AddMemoryAuditLog(MemoryAuditLog entry)
    {
        dbContext.MemoryAuditLogs.Add(entry);
    }

    public void AddToolAuditLog(ToolAuditLog entry)
    {
        dbContext.ToolAuditLogs.Add(entry);
    }

    public async Task<IReadOnlyList<MemoryAuditLog>> GetMemoryAuditLogsAsync(
        string ownerId,
        Guid? memoryId,
        Guid? conversationId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = ownerId.Trim();
        var limit = take <= 0 ? 50 : Math.Min(take, 200);
        var query = dbContext.MemoryAuditLogs.Where(entry => entry.OwnerId == normalizedOwnerId);
        if (memoryId is not null)
        {
            query = query.Where(entry => entry.MemoryId == memoryId);
        }

        if (conversationId is not null)
        {
            query = query.Where(entry => entry.ConversationId == conversationId);
        }

        return await query
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ToolAuditLog>> GetToolAuditLogsAsync(
        string ownerId,
        Guid? conversationId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerId = ownerId.Trim();
        var limit = take <= 0 ? 50 : Math.Min(take, 200);
        var query = dbContext.ToolAuditLogs.Where(entry => entry.OwnerId == normalizedOwnerId);
        if (conversationId is not null)
        {
            query = query.Where(entry => entry.ConversationId == conversationId);
        }

        return await query
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryEntity>> GetExpiredActiveMemoriesAsync(
        DateTimeOffset now,
        int take,
        CancellationToken cancellationToken = default)
    {
        var limit = NormalizeJobLimit(take);

        return await dbContext.Memories
            .Where(memory => memory.Status == MemoryStatus.Active)
            .Where(memory => memory.ValidUntil != null && memory.ValidUntil <= now)
            .OrderBy(memory => memory.ValidUntil)
            .ThenBy(memory => memory.CreatedAt)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<DatabaseRuntimeInfo> GetDatabaseRuntimeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            var canConnect = await dbContext.Database.CanConnectAsync(timeout.Token);
            if (!canConnect)
            {
                return new DatabaseRuntimeInfo(false, null);
            }

            var applied = await dbContext.Database.GetAppliedMigrationsAsync(timeout.Token);
            return new DatabaseRuntimeInfo(true, applied.OrderBy(migration => migration).LastOrDefault());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new DatabaseRuntimeInfo(false, null);
        }
        catch
        {
            return new DatabaseRuntimeInfo(false, null);
        }
    }

    public async Task<RuntimeQueueCounts> GetRuntimeQueueCountsAsync(CancellationToken cancellationToken = default)
    {
        var pendingJobs = await dbContext.IngestionJobs
            .CountAsync(job => job.Status == IngestionJobStatus.Pending, cancellationToken);
        var processingJobs = await dbContext.IngestionJobs
            .CountAsync(job => job.Status == IngestionJobStatus.Processing, cancellationToken);
        var pendingCandidates = await dbContext.MemoryCandidates
            .CountAsync(candidate => candidate.Status == MemoryCandidateStatus.Pending, cancellationToken);
        var pendingConflicts = await dbContext.MemoryConflicts
            .CountAsync(conflict => conflict.Status == MemoryConflictStatus.Pending, cancellationToken);

        return new RuntimeQueueCounts(pendingJobs, processingJobs, pendingCandidates, pendingConflicts);
    }

    public async Task<IReadOnlyList<MemoryCandidate>> GetPendingCandidatesForRetentionAsync(
        DateTimeOffset now,
        DateTimeOffset? staleBefore,
        decimal maxConfidence,
        decimal maxImportance,
        int take,
        CancellationToken cancellationToken = default)
    {
        var limit = NormalizeJobLimit(take);
        var query = dbContext.MemoryCandidates
            .Where(candidate => candidate.Status == MemoryCandidateStatus.Pending);

        if (staleBefore is DateTimeOffset cutoff)
        {
            query = query.Where(candidate =>
                (candidate.ValidUntil != null && candidate.ValidUntil <= now)
                || ((candidate.LastEvidenceAt ?? candidate.UpdatedAt) < cutoff
                    && candidate.Confidence < maxConfidence
                    && candidate.Importance < maxImportance));
        }
        else
        {
            query = query.Where(candidate =>
                candidate.ValidUntil != null && candidate.ValidUntil <= now);
        }

        return await query
            .OrderBy(candidate => candidate.UpdatedAt)
            .ThenBy(candidate => candidate.CreatedAt)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<int> RemoveCompletedIngestionJobsAsync(
        DateTimeOffset cutoff,
        int take,
        CancellationToken cancellationToken = default)
    {
        var limit = NormalizeJobLimit(take);
        var jobs = await dbContext.IngestionJobs
            .Where(job =>
                (job.Status == IngestionJobStatus.Succeeded || job.Status == IngestionJobStatus.Skipped)
                && job.UpdatedAt < cutoff)
            .OrderBy(job => job.UpdatedAt)
            .ThenBy(job => job.CreatedAt)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        if (jobs.Length == 0)
        {
            return 0;
        }

        dbContext.IngestionJobs.RemoveRange(jobs);
        return jobs.Length;
    }

    public Task<MemoryIngestionJob?> ClaimNextIngestionJobAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteInTransactionAsync(async () =>
        {
            var now = DateTimeOffset.UtcNow;
            var pending = nameof(IngestionJobStatus.Pending);
            var processing = nameof(IngestionJobStatus.Processing);

            var job = await dbContext.IngestionJobs
                .FromSql($"""
                    SELECT *
                    FROM memory_ingestion_jobs
                    WHERE status = {pending}
                       OR (
                            status = {processing}
                        AND locked_until IS NOT NULL
                        AND locked_until < {now}
                       )
                    ORDER BY created_at
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                    """)
                .FirstOrDefaultAsync(cancellationToken);

            if (job is null)
            {
                return null;
            }

            job.MarkProcessing(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return job;
        }, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<MemoryEntity> ApplyLookup(IQueryable<MemoryEntity> query, MemoryLookup lookup)
    {
        var ownerId = lookup.OwnerId.Trim();
        var includeConversation = lookup.IncludeConversationScope && lookup.ConversationId is not null;
        var includeUser = lookup.IncludeUserScope;
        var conversationId = lookup.ConversationId;

        return query
            .Where(memory => memory.OwnerId == ownerId)
            .Where(memory =>
                (includeConversation && memory.Scope == MemoryScope.Conversation && memory.ConversationId == conversationId)
                || (includeUser && memory.Scope == MemoryScope.User));
    }

    private static IQueryable<MemoryEntity> ApplyActiveFilter(
        IQueryable<MemoryEntity> query,
        DateTimeOffset now)
    {
        return query
            .Where(memory => memory.Status == MemoryStatus.Active)
            .Where(memory => memory.ValidFrom == null || memory.ValidFrom <= now)
            .Where(memory => memory.ValidUntil == null || memory.ValidUntil > now);
    }

    private static int NormalizeJobLimit(int take)
    {
        return take <= 0 ? 50 : Math.Min(take, 200);
    }

    private sealed class MemoryDistanceRow
    {
        public Guid Id { get; init; }
        public double Distance { get; init; }
    }
}
