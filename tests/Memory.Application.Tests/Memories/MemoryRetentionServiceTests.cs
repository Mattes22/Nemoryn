namespace Memory.Application.Tests.Memories;

using Microsoft.Extensions.Options;
using Memory.Application.Configuration;
using Memory.Application.Memories;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Conversations;
using Memory.Domain.Ingestion;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

public sealed class MemoryRetentionServiceTests
{
    [Fact]
    public async Task Apply_archives_expired_active_memories()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-1");
        var expired = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User lives in Brno until June.",
            MemoryType.Fact,
            0.8m,
            0.9m,
            validUntil: now.AddDays(-1));
        var current = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User prefers Czech.",
            MemoryType.Preference,
            0.8m,
            0.9m,
            validUntil: now.AddDays(30));
        var openEnded = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User likes tea.",
            MemoryType.Preference,
            0.8m,
            0.9m);
        store.AddConversation(conversation);
        store.AddMemory(expired);
        store.AddMemory(current);
        store.AddMemory(openEnded);

        var result = await CreateService(store).ApplyAsync();

        Assert.Equal(1, result.ArchivedMemories);
        Assert.Equal(MemoryStatus.Archived, expired.Status);
        Assert.Equal(MemoryStatus.Active, current.Status);
        Assert.Equal(MemoryStatus.Active, openEnded.Status);
        Assert.Contains(store.AuditLogs, entry =>
            entry.MemoryId == expired.Id
            && entry.Action == MemoryAuditAction.Forgotten
            && entry.ActorKind == MemoryAuditActorKind.Retention);
    }

    [Fact]
    public async Task Apply_archives_expired_pinned_memories()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-1");
        var expiredPinned = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User is in Prague until last month.",
            MemoryType.Fact,
            0.9m,
            0.95m,
            validUntil: DateTimeOffset.UtcNow.AddDays(-2),
            isPinned: true);
        store.AddConversation(conversation);
        store.AddMemory(expiredPinned);

        var result = await CreateService(store).ApplyAsync();

        Assert.Equal(1, result.ArchivedMemories);
        Assert.Equal(MemoryStatus.Archived, expiredPinned.Status);
        Assert.False(expiredPinned.IsPinned);
    }

    [Fact]
    public async Task Apply_discards_stale_weak_pending_candidates()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-1");
        var staleWeak = new MemoryCandidate(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User maybe likes jazz.",
            MemoryType.Preference,
            0.2m,
            0.3m,
            now: now.AddDays(-20));
        var staleStrong = new MemoryCandidate(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User prefers dark mode.",
            MemoryType.Preference,
            0.8m,
            0.9m,
            now: now.AddDays(-20));
        var recentWeak = new MemoryCandidate(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User might like hiking.",
            MemoryType.Preference,
            0.2m,
            0.3m,
            now: now.AddHours(-1));
        store.AddConversation(conversation);
        store.AddMemoryCandidate(staleWeak);
        store.AddMemoryCandidate(staleStrong);
        store.AddMemoryCandidate(recentWeak);

        var result = await CreateService(store).ApplyAsync();

        Assert.Equal(1, result.DiscardedCandidates);
        Assert.Equal(MemoryCandidateStatus.Rejected, staleWeak.Status);
        Assert.Equal(MemoryCandidateStatus.Pending, staleStrong.Status);
        Assert.Equal(MemoryCandidateStatus.Pending, recentWeak.Status);
        Assert.Contains(store.AuditLogs, entry =>
            entry.CandidateId == staleWeak.Id
            && entry.Action == MemoryAuditAction.Rejected
            && entry.ActorKind == MemoryAuditActorKind.Retention);
    }

    [Fact]
    public async Task Apply_discards_expired_pending_candidates()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-1");
        var expired = new MemoryCandidate(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User is traveling until last week.",
            MemoryType.Fact,
            0.9m,
            0.95m,
            validUntil: now.AddDays(-1),
            now: now.AddHours(-1));
        store.AddConversation(conversation);
        store.AddMemoryCandidate(expired);

        var result = await CreateService(store, staleCandidateAgeDays: 0).ApplyAsync();

        Assert.Equal(1, result.DiscardedCandidates);
        Assert.Equal(MemoryCandidateStatus.Rejected, expired.Status);
        Assert.Contains(store.AuditLogs, entry =>
            entry.CandidateId == expired.Id
            && entry.Reason == "Expired memory candidate discarded by retention.");
    }

    [Fact]
    public async Task Apply_closes_pending_conflicts_for_discarded_candidates()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-1");
        var memory = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User prefers tea.",
            MemoryType.Preference,
            0.9m,
            0.9m);
        var candidate = new MemoryCandidate(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User maybe prefers coffee.",
            MemoryType.Preference,
            0.2m,
            0.3m,
            now: now.AddDays(-20));
        var conflict = new MemoryConflict(
            conversation.OwnerId,
            conversation.Id,
            candidate.Id,
            memory.Id,
            0.4m,
            "Possible contradiction.");
        store.AddConversation(conversation);
        store.AddMemory(memory);
        store.AddMemoryCandidate(candidate);
        store.AddMemoryConflict(conflict);

        var result = await CreateService(store).ApplyAsync();

        Assert.Equal(1, result.DiscardedCandidates);
        Assert.Equal(MemoryConflictStatus.ExistingKept, conflict.Status);
        Assert.Contains(store.AuditLogs, entry =>
            entry.ConflictId == conflict.Id
            && entry.Action == MemoryAuditAction.ConflictKept
            && entry.ActorKind == MemoryAuditActorKind.Retention);
    }

    [Fact]
    public async Task Apply_deletes_old_completed_ingestion_jobs()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-1");
        var oldSucceededMessage = new Message(conversation.Id, MessageRole.User, "Old succeeded", 1);
        var recentSucceededMessage = new Message(conversation.Id, MessageRole.User, "Recent succeeded", 2);
        var oldSkippedMessage = new Message(conversation.Id, MessageRole.User, "Old skipped", 3);
        var oldFailedMessage = new Message(conversation.Id, MessageRole.User, "Old failed", 4);
        var pendingMessage = new Message(conversation.Id, MessageRole.User, "Pending", 5);
        var oldSucceeded = new MemoryIngestionJob(conversation.Id, oldSucceededMessage.Id, now.AddDays(-10));
        oldSucceeded.MarkSucceeded(now.AddDays(-10));
        var recentSucceeded = new MemoryIngestionJob(conversation.Id, recentSucceededMessage.Id, now.AddDays(-1));
        recentSucceeded.MarkSucceeded(now.AddDays(-1));
        var oldSkipped = new MemoryIngestionJob(conversation.Id, oldSkippedMessage.Id, now.AddDays(-10));
        oldSkipped.MarkSkipped("No extractable memories.", now.AddDays(-10));
        var oldFailed = new MemoryIngestionJob(conversation.Id, oldFailedMessage.Id, now.AddDays(-10));
        oldFailed.MarkProcessing(now.AddDays(-10));
        oldFailed.MarkProcessing(now.AddDays(-10));
        oldFailed.MarkProcessing(now.AddDays(-10));
        oldFailed.MarkFailed("Extractor failed.", now.AddDays(-10));
        var pending = new MemoryIngestionJob(conversation.Id, pendingMessage.Id, now.AddDays(-10));
        store.AddConversation(conversation);
        store.AddMessage(oldSucceededMessage);
        store.AddMessage(recentSucceededMessage);
        store.AddMessage(oldSkippedMessage);
        store.AddMessage(oldFailedMessage);
        store.AddMessage(pendingMessage);
        store.AddIngestionJob(oldSucceeded);
        store.AddIngestionJob(recentSucceeded);
        store.AddIngestionJob(oldSkipped);
        store.AddIngestionJob(oldFailed);
        store.AddIngestionJob(pending);

        var result = await CreateService(store).ApplyAsync();

        Assert.Equal(2, result.DeletedJobs);
        Assert.DoesNotContain(store.Jobs, job => job.Id == oldSucceeded.Id);
        Assert.DoesNotContain(store.Jobs, job => job.Id == oldSkipped.Id);
        Assert.Contains(store.Jobs, job => job.Id == recentSucceeded.Id);
        Assert.Contains(store.Jobs, job => job.Id == oldFailed.Id);
        Assert.Contains(store.Jobs, job => job.Id == pending.Id);
    }

    [Fact]
    public async Task Apply_skips_job_deletion_when_retention_is_disabled()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-1");
        var message = new Message(conversation.Id, MessageRole.User, "Old succeeded", 1);
        var job = new MemoryIngestionJob(conversation.Id, message.Id, now.AddDays(-30));
        job.MarkSucceeded(now.AddDays(-30));
        store.AddConversation(conversation);
        store.AddMessage(message);
        store.AddIngestionJob(job);

        var result = await CreateService(store, completedJobAgeDays: 0).ApplyAsync();

        Assert.Equal(0, result.DeletedJobs);
        Assert.Contains(store.Jobs, item => item.Id == job.Id);
    }

    private static MemoryRetentionService CreateService(
        FakeMemoryStore store,
        int staleCandidateAgeDays = 14,
        int completedJobAgeDays = 7)
    {
        return new MemoryRetentionService(
            store,
            Options.Create(new MemoryAiOptions
            {
                AutoPromoteConfidence = 0.82m,
                AutoPromoteImportance = 0.65m,
                RetentionBatchSize = 50,
                StaleCandidateAgeDays = staleCandidateAgeDays,
                CompletedIngestionJobAgeDays = completedJobAgeDays
            }));
    }
}
