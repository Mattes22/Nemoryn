namespace Memory.Application.Tests.Memories;

using Microsoft.Extensions.Options;
using Memory.Application.Configuration;
using Memory.Application.Exceptions;
using Memory.Application.Memories;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Conversations;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

public sealed class MemoryCandidateServiceTests
{
    [Fact]
    public async Task Promote_writes_explicit_memory()
    {
        var (store, candidate) = SeedPendingCandidate();
        var service = CreateService(store, embeddingAvailable: false);

        var memory = await service.PromoteAsync(candidate.Id, new PromoteMemoryCandidateRequest());

        Assert.Equal(MemoryOrigin.Explicit, memory.Origin);
        Assert.False(memory.IsPinned);
        Assert.Equal(MemoryCandidateStatus.Promoted, candidate.Status);
        Assert.Equal(memory.Id, candidate.PromotedMemoryId);
    }

    [Fact]
    public async Task Promote_refuses_when_a_pending_conflict_exists()
    {
        var (store, candidate) = SeedPendingCandidate();
        var existing = new MemoryEntity(
            candidate.OwnerId,
            candidate.ConversationId,
            MemoryScope.User,
            "User likes tea.",
            MemoryType.Preference,
            0.9m,
            0.9m,
            origin: MemoryOrigin.Explicit,
            isPinned: true);
        store.AddMemory(existing);
        store.AddMemoryConflict(new MemoryConflict(
            candidate.OwnerId,
            candidate.ConversationId,
            candidate.Id,
            existing.Id,
            0.9m,
            "Pinned preference."));

        var service = CreateService(store, embeddingAvailable: false);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.PromoteAsync(candidate.Id, new PromoteMemoryCandidateRequest()));

        Assert.Contains("pending memory conflict", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(MemoryCandidateStatus.Pending, candidate.Status);
        Assert.DoesNotContain(store.Memories, item => item.Id != existing.Id);
    }

    [Fact]
    public async Task Promote_creates_conflict_instead_of_overwriting_pinned_memory()
    {
        var (store, candidate) = SeedPendingCandidate("User likes coffee.");
        var pinned = new MemoryEntity(
            candidate.OwnerId,
            candidate.ConversationId,
            MemoryScope.User,
            "User likes tea.",
            MemoryType.Preference,
            0.9m,
            0.95m,
            origin: MemoryOrigin.Explicit,
            isPinned: true);
        pinned.SetEmbedding(await Embed());
        store.AddMemory(pinned);

        var service = CreateService(store);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.PromoteAsync(candidate.Id, new PromoteMemoryCandidateRequest()));

        Assert.Contains("cannot be superseded", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(MemoryCandidateStatus.Pending, candidate.Status);
        Assert.Equal(MemoryStatus.Active, pinned.Status);
        var conflict = Assert.Single(store.Conflicts);
        Assert.Equal(candidate.Id, conflict.CandidateId);
        Assert.Equal(pinned.Id, conflict.ConflictingMemoryId);
    }

    [Fact]
    public async Task Promote_supersedes_similar_unprotected_memory()
    {
        var (store, candidate) = SeedPendingCandidate("User likes coffee.");
        var existing = new MemoryEntity(
            candidate.OwnerId,
            candidate.ConversationId,
            MemoryScope.User,
            "User likes tea.",
            MemoryType.Preference,
            0.6m,
            0.6m);
        existing.SetEmbedding(await Embed());
        store.AddMemory(existing);

        var service = CreateService(store);
        var memory = await service.PromoteAsync(candidate.Id, new PromoteMemoryCandidateRequest());

        Assert.Equal(MemoryStatus.Superseded, existing.Status);
        Assert.Equal(memory.Id, existing.SupersededByMemoryId);
        Assert.Equal(MemoryOrigin.Explicit, memory.Origin);
    }

    [Fact]
    public async Task Reject_marks_candidate_rejected()
    {
        var (store, candidate) = SeedPendingCandidate();
        var service = CreateService(store, embeddingAvailable: false);

        var response = await service.RejectAsync(candidate.Id);

        Assert.Equal(MemoryCandidateStatus.Rejected, response.Status);
        Assert.Equal(MemoryCandidateStatus.Rejected, candidate.Status);
    }

    [Fact]
    public async Task Reject_resolves_pending_conflicts()
    {
        var (store, candidate) = SeedPendingCandidate();
        var existing = new MemoryEntity(
            candidate.OwnerId,
            candidate.ConversationId,
            MemoryScope.User,
            "User likes tea.",
            MemoryType.Preference,
            0.9m,
            0.9m,
            origin: MemoryOrigin.Explicit,
            isPinned: true);
        store.AddMemory(existing);
        var conflict = new MemoryConflict(
            candidate.OwnerId,
            candidate.ConversationId,
            candidate.Id,
            existing.Id,
            0.9m,
            "Pinned preference.");
        store.AddMemoryConflict(conflict);

        var service = CreateService(store, embeddingAvailable: false);
        var response = await service.RejectAsync(candidate.Id);

        Assert.Equal(MemoryCandidateStatus.Rejected, response.Status);
        Assert.Equal(MemoryConflictStatus.ExistingKept, conflict.Status);
        Assert.Equal(MemoryStatus.Active, existing.Status);
    }

    private static MemoryCandidateService CreateService(FakeMemoryStore store, bool embeddingAvailable = true)
    {
        return new MemoryCandidateService(
            store,
            new FakeEmbeddingProvider { IsAvailable = embeddingAvailable },
            Options.Create(new MemoryAiOptions
            {
                EmbeddingDimensions = 8,
                SearchLimit = 10,
                SimilarityThreshold = 0.0f
            }));
    }

    private static (FakeMemoryStore Store, MemoryCandidate Candidate) SeedPendingCandidate(
        string content = "User likes espresso.")
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);

        var candidate = new MemoryCandidate(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            content,
            MemoryType.Preference,
            0.8m,
            0.9m);
        candidate.AddEvidence(0.8m, 0.9m);
        store.AddMemoryCandidate(candidate);

        return (store, candidate);
    }

    private static Task<IReadOnlyList<float>> Embed()
    {
        return Task.FromResult<IReadOnlyList<float>>([1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f]);
    }
}
