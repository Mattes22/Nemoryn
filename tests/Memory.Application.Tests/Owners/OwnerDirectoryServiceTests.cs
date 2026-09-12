namespace Memory.Application.Tests.Owners;

using Memory.Application.Owners;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Conversations;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

public sealed class OwnerDirectoryServiceTests
{
    [Fact]
    public async Task List_merges_conversations_and_active_memories()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-1");
        store.AddConversation(conversation);
        store.AddConversation(new Conversation("matej", "chat-2"));
        store.AddConversation(new Conversation("anonymous", "chat-a"));
        store.AddMemory(new MemoryEntity(
            "matej",
            conversation.Id,
            MemoryScope.User,
            "User prefers Czech.",
            MemoryType.Preference,
            0.9m,
            0.9m));

        var owners = await new OwnerDirectoryService(store).ListAsync();

        var matej = Assert.Single(owners, owner => owner.OwnerId == "matej");
        var anonymous = Assert.Single(owners, owner => owner.OwnerId == "anonymous");
        Assert.Equal(2, matej.ConversationCount);
        Assert.Equal(1, matej.MemoryCount);
        Assert.Equal(1, anonymous.ConversationCount);
        Assert.Equal(0, anonymous.MemoryCount);
    }

    [Fact]
    public async Task List_orders_by_last_activity()
    {
        var store = new FakeMemoryStore();
        store.AddConversation(new Conversation("older", "c1", now: DateTimeOffset.Parse("2026-01-01T00:00:00Z")));
        store.AddConversation(new Conversation("newer", "c2", now: DateTimeOffset.Parse("2026-09-01T00:00:00Z")));

        var owners = await new OwnerDirectoryService(store).ListAsync();

        Assert.Equal(["newer", "older"], owners.Select(owner => owner.OwnerId).ToArray());
    }

    [Fact]
    public async Task List_is_empty_when_store_has_no_owners()
    {
        var owners = await new OwnerDirectoryService(new FakeMemoryStore()).ListAsync();

        Assert.Empty(owners);
    }
}
