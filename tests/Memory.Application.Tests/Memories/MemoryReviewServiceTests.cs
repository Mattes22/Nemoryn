namespace Memory.Application.Tests.Memories;

using Microsoft.Extensions.Options;
using Memory.Application.Configuration;
using Memory.Application.Exceptions;
using Memory.Application.Memories;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Conversations;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

public sealed class MemoryReviewServiceTests
{
    [Fact]
    public async Task Review_combines_active_pending_conflicts_and_cleanup()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-1");
        var stable = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User prefers Czech.",
            MemoryType.Preference,
            0.9m,
            0.9m);
        var noisy = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User ID is matej.",
            MemoryType.Fact,
            0.8m,
            0.9m);
        var candidate = new MemoryCandidate(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User drinks tea.",
            MemoryType.Preference,
            0.7m,
            0.7m);
        store.AddConversation(conversation);
        store.AddMemory(stable);
        store.AddMemory(noisy);
        store.AddMemoryCandidate(candidate);
        store.AddMemoryConflict(new MemoryConflict(
            conversation.OwnerId,
            conversation.Id,
            candidate.Id,
            stable.Id,
            0.8m,
            "Preference clash."));

        var review = await CreateService(store).GetAsync("matej");

        Assert.Equal("matej", review.OwnerId);
        Assert.Null(review.ConversationId);
        Assert.Equal(2, review.ActiveMemories.Count);
        Assert.Contains(review.ActiveMemories, memory => memory.Id == stable.Id);
        Assert.Contains(review.PendingCandidates, item => item.Id == candidate.Id);
        Assert.Single(review.PendingConflicts);
        Assert.Contains(review.Cleanup.Suggestions, suggestion => suggestion.MemoryId == noisy.Id);
        Assert.Equal(
            review.PendingCandidates.Count + review.PendingConflicts.Count + review.Cleanup.Suggestions.Count,
            review.AttentionCount);
        Assert.True(review.AttentionCount >= 3);
    }

    [Fact]
    public async Task Review_can_filter_to_one_conversation()
    {
        var store = new FakeMemoryStore();
        var first = new Conversation("matej", "chat-1");
        var second = new Conversation("matej", "chat-2");
        store.AddConversation(first);
        store.AddConversation(second);
        store.AddMemory(new MemoryEntity(
            first.OwnerId,
            first.Id,
            MemoryScope.Conversation,
            "Talked about tea in this chat.",
            MemoryType.Fact,
            0.8m,
            0.8m));
        store.AddMemoryCandidate(new MemoryCandidate(
            second.OwnerId,
            second.Id,
            MemoryScope.Conversation,
            "Other chat candidate.",
            MemoryType.Fact,
            0.6m,
            0.6m));

        var review = await CreateService(store).GetAsync("matej", first.Id);

        Assert.Equal(first.Id, review.ConversationId);
        Assert.Contains(review.ActiveMemories, memory => memory.Content.Contains("tea", StringComparison.Ordinal));
        Assert.Empty(review.PendingCandidates);
        Assert.Empty(review.PendingConflicts);
    }

    [Fact]
    public async Task Review_rejects_conversation_owned_by_someone_else()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("other", "chat-1");
        store.AddConversation(conversation);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(store).GetAsync("matej", conversation.Id));
    }

    private static MemoryReviewService CreateService(FakeMemoryStore store)
    {
        var options = Options.Create(new MemoryAiOptions
        {
            EmbeddingDimensions = 8,
            SearchLimit = 10
        });
        var embeddings = new FakeEmbeddingProvider { IsAvailable = false };
        return new MemoryReviewService(
            store,
            new MemoryCandidateService(store, embeddings, options),
            new MemoryConflictService(store, embeddings, options),
            new MemoryCleanupService(store, options));
    }
}
