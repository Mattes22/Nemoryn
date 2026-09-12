namespace Memory.Application.Tests.Memories;

using Microsoft.Extensions.Options;
using Memory.Application.Configuration;
using Memory.Application.Exceptions;
using Memory.Application.Memories;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Conversations;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

public sealed class MemoryServiceTests
{
    [Fact]
    public async Task Create_rejects_source_message_from_another_conversation()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        var other = new Conversation("user-1", "chat-2");
        var foreignMessage = new Message(other.Id, MessageRole.User, "Hi", 1);
        store.AddConversation(conversation);
        store.AddConversation(other);
        store.AddMessage(foreignMessage);

        var service = CreateService(store);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateMemoryRequest(
                conversation.Id,
                "User likes tea.",
                MemoryType.Preference,
                0.8m,
                0.9m,
                foreignMessage.Id,
                null,
                null,
                null,
                null)));

        Assert.Contains("does not belong", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetActive_hides_expired_and_superseded_memories()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);

        var active = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.Conversation,
            "User likes tea.",
            MemoryType.Preference,
            0.9m,
            0.8m);
        var expired = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.Conversation,
            "Temporary.",
            MemoryType.Fact,
            0.5m,
            0.5m,
            validFrom: DateTimeOffset.UtcNow.AddDays(-2),
            validUntil: DateTimeOffset.UtcNow.AddDays(-1));
        var superseded = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.Conversation,
            "Old tea fact.",
            MemoryType.Preference,
            0.4m,
            0.4m);
        superseded.Supersede(active.Id);

        store.AddMemory(active);
        store.AddMemory(expired);
        store.AddMemory(superseded);

        var service = CreateService(store, embeddingAvailable: false);
        var memories = await service.GetActiveForConversationAsync(conversation.Id);

        Assert.Single(memories);
        Assert.Equal(active.Id, memories[0].Id);
    }

    [Fact]
    public async Task Retrieve_includes_user_scope_from_other_conversations()
    {
        var store = new FakeMemoryStore();
        var current = new Conversation("user-1", "chat-1");
        var previous = new Conversation("user-1", "chat-0");
        store.AddConversation(current);
        store.AddConversation(previous);

        var userMemory = new MemoryEntity(
            current.OwnerId,
            previous.Id,
            MemoryScope.User,
            "User prefers dark mode.",
            MemoryType.Preference,
            0.9m,
            0.9m);
        var foreign = new MemoryEntity(
            "someone-else",
            previous.Id,
            MemoryScope.User,
            "Someone else likes light mode.",
            MemoryType.Preference,
            1.0m,
            1.0m);

        store.AddMemory(userMemory);
        store.AddMemory(foreign);

        var service = CreateService(store, embeddingAvailable: false);
        var ranked = await service.RetrieveAsync(
            current.Id,
            new SearchMemoriesRequest(string.Empty, 10));

        Assert.Single(ranked);
        Assert.Equal(userMemory.Id, ranked[0].Memory.Id);
        Assert.Equal(MemoryScope.User, ranked[0].Memory.Scope);
    }

    [Fact]
    public async Task RetrieveStable_always_includes_pinned_core_memory()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);

        var pinned = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User name is Matej.",
            MemoryType.Fact,
            0.95m,
            0.95m);
        pinned.Pin();

        var weak = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.Conversation,
            "Talking about lunch.",
            MemoryType.Summary,
            0.2m,
            0.2m);

        store.AddMemory(pinned);
        store.AddMemory(weak);

        var service = CreateService(store, embeddingAvailable: false);
        var stable = await service.RetrieveStableAsync(
            conversation.Id,
            new SearchMemoriesRequest("lunch plans", 5));

        Assert.Contains(stable.Core, item => item.Memory.Id == pinned.Id && item.Memory.IsPinned);
    }

    [Fact]
    public async Task RetrieveStable_filters_weak_relevant_memory_when_embedding_is_unavailable()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);

        var weak = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.Conversation,
            "User once mentioned lunch.",
            MemoryType.Summary,
            0.2m,
            0.2m);
        store.AddMemory(weak);

        var service = CreateService(
            store,
            embeddingAvailable: false,
            memoryAiOptions: new MemoryAiOptions
            {
                EmbeddingDimensions = 8,
                SearchLimit = 10,
                MinRelevantScore = 0.70d
            });

        var stable = await service.RetrieveStableAsync(
            conversation.Id,
            new SearchMemoriesRequest(string.Empty, 10));

        Assert.Empty(stable.Core);
        Assert.Empty(stable.Relevant);
    }

    [Fact]
    public async Task RetrieveStable_limits_relevant_memories_after_core_exclusion()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);

        for (var index = 0; index < 5; index++)
        {
            store.AddMemory(new MemoryEntity(
                conversation.OwnerId,
                conversation.Id,
                MemoryScope.Conversation,
                $"Project detail {index}.",
                MemoryType.Fact,
                0.9m,
                0.9m));
        }

        var service = CreateService(
            store,
            embeddingAvailable: false,
            memoryAiOptions: new MemoryAiOptions
            {
                EmbeddingDimensions = 8,
                SearchLimit = 10,
                MaxRelevantMemories = 2,
                MinRelevantScore = 0.70d
            });

        var stable = await service.RetrieveStableAsync(
            conversation.Id,
            new SearchMemoriesRequest(string.Empty, 10));

        Assert.Equal(2, stable.Relevant.Count);
    }

    [Fact]
    public async Task Forget_hides_memory_from_active_owner_list()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);
        var memory = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User prefers dark mode.",
            MemoryType.Preference,
            0.9m,
            0.9m);
        store.AddMemory(memory);

        var service = CreateService(store, embeddingAvailable: false);
        await service.ForgetAsync(memory.Id);
        var remaining = await service.GetActiveForOwnerAsync("user-1");

        Assert.Empty(remaining);
    }

    [Fact]
    public async Task GetActive_throws_when_conversation_is_missing()
    {
        var service = CreateService(new FakeMemoryStore(), embeddingAvailable: false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetActiveForConversationAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Search_requires_query()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);
        var service = CreateService(store);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SearchAsync(conversation.Id, new SearchMemoriesRequest("  ", 5)));
    }

    private static MemoryService CreateService(
        FakeMemoryStore store,
        bool embeddingAvailable = true,
        MemoryAiOptions? memoryAiOptions = null)
    {
        var embeddings = new FakeEmbeddingProvider { IsAvailable = embeddingAvailable };
        var options = Options.Create(memoryAiOptions ?? new MemoryAiOptions
        {
            EmbeddingDimensions = 8,
            SearchLimit = 10
        });

        return new MemoryService(store, embeddings, options);
    }
}
