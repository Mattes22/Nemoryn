namespace Memory.Application.Tests.Conversations;

using Memory.Application.Conversations;
using Memory.Application.Exceptions;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Conversations;
using Memory.Domain.Ingestion;

public sealed class ConversationServiceTests
{
    [Fact]
    public async Task Create_then_upsert_keeps_title_when_null()
    {
        var store = new FakeMemoryStore();
        var service = new ConversationService(store);

        var created = await service.CreateAsync(new CreateConversationRequest("user-1", "chat-1", "Hello"));
        var upserted = await service.UpsertByExternalIdAsync("chat-1", new UpsertConversationRequest("user-1", null));

        Assert.Equal(created.Id, upserted.Conversation.Id);
        Assert.False(upserted.Created);
        Assert.Equal("Hello", upserted.Conversation.Title);
        Assert.Equal("user-1", upserted.Conversation.OwnerId);
    }

    [Fact]
    public async Task Create_rejects_duplicate_external_id_for_same_owner()
    {
        var service = new ConversationService(new FakeMemoryStore());
        await service.CreateAsync(new CreateConversationRequest("user-1", "chat-1", "Hello"));

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(new CreateConversationRequest("user-1", "chat-1", "Again")));
    }

    [Fact]
    public async Task AddMessage_uses_conversation_counter_and_enqueues_user_ingestion()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);
        var service = new ConversationService(store);

        var first = await service.AddMessageAsync(
            conversation.Id,
            new AddMessageRequest(MessageRole.User, "Hi", "m1", null));
        var second = await service.AddMessageAsync(
            conversation.Id,
            new AddMessageRequest(MessageRole.Assistant, "Hello", "m2", null));

        Assert.Equal(1, first.SequenceNumber);
        Assert.Equal(2, second.SequenceNumber);
        Assert.Equal(IngestionJobStatus.Pending, first.IngestionStatus);
        Assert.NotNull(first.IngestionJobId);
        Assert.Null(second.IngestionJobId);
        Assert.Null(second.IngestionStatus);
        Assert.Single(store.Jobs);
    }

    [Fact]
    public async Task AddMessage_can_skip_ingestion_and_enqueue_later()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);
        var service = new ConversationService(store);

        var message = await service.AddMessageAsync(
            conversation.Id,
            new AddMessageRequest(MessageRole.User, "Hi", null, null, EnqueueIngestion: false));

        Assert.Null(message.IngestionJobId);
        Assert.Empty(store.Jobs);

        var enqueued = await service.EnqueueUserMessageIngestionAsync(conversation.Id, message.Id);

        Assert.NotNull(enqueued.IngestionJobId);
        Assert.Equal(IngestionJobStatus.Pending, enqueued.IngestionStatus);
        Assert.Single(store.Jobs);
    }

    [Fact]
    public async Task ListForOwner_returns_only_that_owners_conversations()
    {
        var store = new FakeMemoryStore();
        store.AddConversation(new Conversation("user-1", "chat-1", "Mine"));
        store.AddConversation(new Conversation("user-2", "chat-2", "Other"));
        var service = new ConversationService(store);

        var conversations = await service.ListForOwnerAsync("user-1");

        var conversation = Assert.Single(conversations);
        Assert.Equal("chat-1", conversation.ExternalId);
        Assert.Equal("Mine", conversation.Title);
    }

    [Fact]
    public async Task GetMessages_returns_conversation_history()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);
        var service = new ConversationService(store);

        await service.AddMessageAsync(
            conversation.Id,
            new AddMessageRequest(MessageRole.User, "Hi", null, null));
        await service.AddMessageAsync(
            conversation.Id,
            new AddMessageRequest(MessageRole.Assistant, "Hello", null, null));

        var messages = await service.GetMessagesAsync(conversation.Id);

        Assert.Equal(2, messages.Count);
        Assert.Equal("Hi", messages[0].Content);
        Assert.Equal("Hello", messages[1].Content);
    }

    [Fact]
    public async Task AddMessage_throws_not_found_for_unknown_conversation()
    {
        var service = new ConversationService(new FakeMemoryStore());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.AddMessageAsync(
                Guid.NewGuid(),
                new AddMessageRequest(MessageRole.User, "Hi", null, null)));
    }
}
