namespace Memory.Application.Memories;

using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.Persistence;
using Memory.Application.Configuration;
using Memory.Domain.Memories;

internal sealed class MemoryRetentionService(
    IMemoryStore memoryStore,
    IOptions<MemoryAiOptions> memoryAiOptions) : IMemoryRetentionService
{
    public async Task<MemoryRetentionResult> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var archivedMemories = await ArchiveExpiredMemoriesAsync(now, cancellationToken);
        var discardedCandidates = await DiscardPendingCandidatesAsync(now, cancellationToken);
        var deletedJobs = await DeleteCompletedJobsAsync(now, cancellationToken);

        return new MemoryRetentionResult(archivedMemories, discardedCandidates, deletedJobs);
    }

    private async Task<int> ArchiveExpiredMemoriesAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var options = memoryAiOptions.Value;
        var memories = await memoryStore.GetExpiredActiveMemoriesAsync(
            now,
            options.RetentionBatchSize,
            cancellationToken);

        foreach (var memory in memories)
        {
            memory.Forget();
            memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForMemory(
                MemoryAuditAction.Forgotten,
                memory,
                MemoryAuditActorKind.Retention,
                "Memory expired and archived by retention."));
        }

        if (memories.Count > 0)
        {
            await memoryStore.SaveChangesAsync(cancellationToken);
        }

        return memories.Count;
    }

    private async Task<int> DiscardPendingCandidatesAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var options = memoryAiOptions.Value;
        DateTimeOffset? staleBefore = options.StaleCandidateAgeDays > 0
            ? now.AddDays(-options.StaleCandidateAgeDays)
            : null;
        var candidates = await memoryStore.GetPendingCandidatesForRetentionAsync(
            now,
            staleBefore,
            options.AutoPromoteConfidence,
            options.AutoPromoteImportance,
            options.RetentionBatchSize,
            cancellationToken);

        foreach (var candidate in candidates)
        {
            var pendingConflicts = await memoryStore.GetPendingMemoryConflictsForCandidateAsync(
                candidate.Id,
                cancellationToken);
            foreach (var conflict in pendingConflicts)
            {
                conflict.MarkExistingKept(now);
                memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForConflict(
                    MemoryAuditAction.ConflictKept,
                    conflict,
                    MemoryAuditActorKind.Retention,
                    "Pending conflict closed because the candidate was discarded by retention."));
            }

            var expired = candidate.ValidUntil is not null && candidate.ValidUntil <= now;
            candidate.Reject(now);
            memoryStore.AddMemoryAuditLog(MemoryAuditLog.ForCandidate(
                MemoryAuditAction.Rejected,
                candidate,
                MemoryAuditActorKind.Retention,
                expired
                    ? "Expired memory candidate discarded by retention."
                    : "Stale weak candidate discarded by retention."));
        }

        if (candidates.Count > 0)
        {
            await memoryStore.SaveChangesAsync(cancellationToken);
        }

        return candidates.Count;
    }

    private async Task<int> DeleteCompletedJobsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var options = memoryAiOptions.Value;
        if (options.CompletedIngestionJobAgeDays <= 0)
        {
            return 0;
        }

        var deleted = await memoryStore.RemoveCompletedIngestionJobsAsync(
            now.AddDays(-options.CompletedIngestionJobAgeDays),
            options.RetentionBatchSize,
            cancellationToken);

        if (deleted > 0)
        {
            await memoryStore.SaveChangesAsync(cancellationToken);
        }

        return deleted;
    }
}
