namespace Memory.Application.Tests.Memories;

using Microsoft.Extensions.Options;
using Memory.Application.Configuration;
using Memory.Application.Memories;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Conversations;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

public sealed class MemoryConflictServiceTests
{
    [Fact]
    public async Task Accept_candidate_writes_explicit_memory_and_supersedes_existing()
    {
        var (store, conflict, candidate, existing) = SeedPendingConflict();
        var service = CreateService(store);

        var memory = await service.AcceptCandidateAsync(conflict.Id);

        Assert.Equal(MemoryOrigin.Explicit, memory.Origin);
        Assert.Equal(MemoryStatus.Superseded, existing.Status);
        Assert.Equal(memory.Id, existing.SupersededByMemoryId);
        Assert.Equal(MemoryCandidateStatus.Promoted, candidate.Status);
        Assert.Equal(MemoryConflictStatus.CandidateAccepted, conflict.Status);
        Assert.False(existing.IsPinned);
    }

    [Fact]
    public async Task Keep_existing_rejects_candidate()
    {
        var (store, conflict, candidate, existing) = SeedPendingConflict();
        var service = CreateService(store);

        var response = await service.KeepExistingAsync(conflict.Id);

        Assert.Equal(MemoryConflictStatus.ExistingKept, response.Status);
        Assert.Equal(MemoryCandidateStatus.Rejected, candidate.Status);
        Assert.Equal(MemoryStatus.Active, existing.Status);
        Assert.True(existing.IsPinned);
    }

    [Fact]
    public async Task Keep_existing_resolves_sibling_conflicts()
    {
        var (store, conflict, candidate, existing) = SeedPendingConflict();
        var other = new MemoryEntity(
            candidate.OwnerId,
            candidate.ConversationId,
            MemoryScope.User,
            "User likes espresso.",
            MemoryType.Preference,
            0.8m,
            0.8m);
        store.AddMemory(other);
        var sibling = new MemoryConflict(
            candidate.OwnerId,
            candidate.ConversationId,
            candidate.Id,
            other.Id,
            0.8m,
            "Another drink preference.");
        store.AddMemoryConflict(sibling);

        var service = CreateService(store);
        var response = await service.KeepExistingAsync(conflict.Id);

        Assert.Equal(MemoryConflictStatus.ExistingKept, response.Status);
        Assert.Equal(MemoryConflictStatus.ExistingKept, sibling.Status);
        Assert.Equal(MemoryCandidateStatus.Rejected, candidate.Status);
        Assert.Equal(MemoryStatus.Active, existing.Status);
        Assert.Equal(MemoryStatus.Active, other.Status);
    }

    [Fact]
    public async Task Accept_candidate_resolves_sibling_conflicts()
    {
        var (store, conflict, candidate, existing) = SeedPendingConflict();
        var other = new MemoryEntity(
            candidate.OwnerId,
            candidate.ConversationId,
            MemoryScope.User,
            "User likes espresso.",
            MemoryType.Preference,
            0.8m,
            0.8m);
        store.AddMemory(other);
        var sibling = new MemoryConflict(
            candidate.OwnerId,
            candidate.ConversationId,
            candidate.Id,
            other.Id,
            0.8m,
            "Another drink preference.");
        store.AddMemoryConflict(sibling);

        var service = CreateService(store);
        var memory = await service.AcceptCandidateAsync(conflict.Id);

        Assert.Equal(MemoryStatus.Superseded, existing.Status);
        Assert.Equal(memory.Id, existing.SupersededByMemoryId);
        Assert.Equal(MemoryStatus.Active, other.Status);
        Assert.Equal(MemoryConflictStatus.CandidateAccepted, conflict.Status);
        Assert.Equal(MemoryConflictStatus.ExistingKept, sibling.Status);
        Assert.Equal(MemoryCandidateStatus.Promoted, candidate.Status);
    }

    private static MemoryConflictService CreateService(FakeMemoryStore store)
    {
        return new MemoryConflictService(
            store,
            new FakeEmbeddingProvider { IsAvailable = false },
            Options.Create(new MemoryAiOptions { EmbeddingDimensions = 8 }));
    }

    private static (FakeMemoryStore Store, MemoryConflict Conflict, MemoryCandidate Candidate, MemoryEntity Existing)
        SeedPendingConflict()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);

        var existing = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User likes tea.",
            MemoryType.Preference,
            0.9m,
            0.95m,
            origin: MemoryOrigin.Explicit,
            isPinned: true);
        store.AddMemory(existing);

        var candidate = new MemoryCandidate(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User likes coffee.",
            MemoryType.Preference,
            0.8m,
            0.9m);
        candidate.AddEvidence(0.8m, 0.9m);
        store.AddMemoryCandidate(candidate);

        var conflict = new MemoryConflict(
            conversation.OwnerId,
            conversation.Id,
            candidate.Id,
            existing.Id,
            0.9m,
            "Pinned preference.");
        store.AddMemoryConflict(conflict);

        return (store, conflict, candidate, existing);
    }
}
