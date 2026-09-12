namespace Memory.Application.Tests.Ingestion;

using Memory.Application.Exceptions;
using Memory.Application.Ingestion;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Conversations;
using Memory.Domain.Ingestion;

public sealed class IngestionJobServiceTests
{
    [Fact]
    public async Task Get_for_conversation_returns_recent_jobs()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        var firstMessage = new Message(conversation.Id, MessageRole.User, "A", 1);
        var secondMessage = new Message(conversation.Id, MessageRole.User, "B", 2);
        var firstJob = new MemoryIngestionJob(conversation.Id, firstMessage.Id, DateTimeOffset.UtcNow.AddMinutes(-1));
        var secondJob = new MemoryIngestionJob(conversation.Id, secondMessage.Id, DateTimeOffset.UtcNow);
        store.AddConversation(conversation);
        store.AddMessage(firstMessage);
        store.AddMessage(secondMessage);
        store.AddIngestionJob(firstJob);
        store.AddIngestionJob(secondJob);

        var service = new IngestionJobService(store);

        var jobs = await service.GetForConversationAsync(conversation.Id);

        Assert.Collection(
            jobs,
            job => Assert.Equal(secondJob.Id, job.Id),
            job => Assert.Equal(firstJob.Id, job.Id));
    }

    [Fact]
    public async Task Get_for_owner_returns_only_owner_jobs()
    {
        var store = new FakeMemoryStore();
        var ownedConversation = new Conversation("user-1", "chat-1");
        var foreignConversation = new Conversation("user-2", "chat-2");
        var ownedMessage = new Message(ownedConversation.Id, MessageRole.User, "A", 1);
        var foreignMessage = new Message(foreignConversation.Id, MessageRole.User, "B", 1);
        var ownedJob = new MemoryIngestionJob(ownedConversation.Id, ownedMessage.Id);
        var foreignJob = new MemoryIngestionJob(foreignConversation.Id, foreignMessage.Id);
        store.AddConversation(ownedConversation);
        store.AddConversation(foreignConversation);
        store.AddMessage(ownedMessage);
        store.AddMessage(foreignMessage);
        store.AddIngestionJob(ownedJob);
        store.AddIngestionJob(foreignJob);

        var service = new IngestionJobService(store);

        var jobs = await service.GetForOwnerAsync("user-1");

        var job = Assert.Single(jobs);
        Assert.Equal(ownedJob.Id, job.Id);
    }

    [Fact]
    public async Task Get_for_unknown_conversation_throws_not_found()
    {
        var service = new IngestionJobService(new FakeMemoryStore());

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetForConversationAsync(Guid.NewGuid()));
    }
}
