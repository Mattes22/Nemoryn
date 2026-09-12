namespace Memory.Application.Tests.Memories;

using Microsoft.Extensions.Options;
using Memory.Application.Configuration;
using Memory.Application.Memories;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Conversations;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

public sealed class MemoryAuditServiceTests
{
    [Fact]
    public async Task Create_and_pin_write_audit_entries()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);
        var memories = CreateMemoryService(store);

        var created = await memories.CreateAsync(new CreateMemoryRequest(
            conversation.Id,
            "User likes tea.",
            MemoryType.Preference,
            0.9m,
            0.9m,
            null,
            null,
            null,
            null,
            null,
            Pin: true));

        await memories.UnpinAsync(created.Id);

        var audit = new MemoryAuditService(store);
        var entries = await audit.GetForOwnerAsync("user-1");

        Assert.Equal(3, entries.Count);
        Assert.Contains(entries, entry =>
            entry.Action == MemoryAuditAction.Created
            && entry.ActorKind == MemoryAuditActorKind.User
            && entry.MemoryId == created.Id);
        Assert.Contains(entries, entry => entry.Action == MemoryAuditAction.Pinned);
        Assert.Contains(entries, entry => entry.Action == MemoryAuditAction.Unpinned);
    }

    [Fact]
    public async Task Forget_is_attributed_to_the_user()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        var memory = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User likes tea.",
            MemoryType.Preference,
            0.9m,
            0.8m);
        store.AddConversation(conversation);
        store.AddMemory(memory);

        await CreateMemoryService(store).ForgetAsync(memory.Id);

        var entry = Assert.Single(store.AuditLogs);
        Assert.Equal(MemoryAuditAction.Forgotten, entry.Action);
        Assert.Equal("user-1", entry.ActorId);
        Assert.Equal(memory.Id, entry.MemoryId);
    }

    [Fact]
    public async Task Reject_candidate_writes_rejected_audit()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        var candidate = new MemoryCandidate(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User drinks coffee.",
            MemoryType.Preference,
            0.7m,
            0.7m);
        store.AddConversation(conversation);
        store.AddMemoryCandidate(candidate);

        var candidates = new MemoryCandidateService(
            store,
            new FakeEmbeddingProvider { IsAvailable = false },
            Options.Create(new MemoryAiOptions { SearchLimit = 10 }));
        await candidates.RejectAsync(candidate.Id);

        var entry = Assert.Single(store.AuditLogs);
        Assert.Equal(MemoryAuditAction.Rejected, entry.Action);
        Assert.Equal(candidate.Id, entry.CandidateId);
        Assert.Equal(MemoryAuditActorKind.User, entry.ActorKind);
    }

    private static MemoryService CreateMemoryService(FakeMemoryStore store)
    {
        return new MemoryService(
            store,
            new FakeEmbeddingProvider { IsAvailable = false },
            Options.Create(new MemoryAiOptions { EmbeddingDimensions = 8, SearchLimit = 10 }));
    }
}
