namespace Memory.Application.Tests.Memories;

using Microsoft.Extensions.Options;
using Memory.Application.Configuration;
using Memory.Application.Memories;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Conversations;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

public sealed class MemoryCleanupServiceTests
{
    [Fact]
    public async Task Preview_suggests_inferred_metadata_and_low_signal_memories()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-1");
        var metadata = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User ID is matej.",
            MemoryType.Fact,
            0.8m,
            0.9m);
        var lowSignal = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User may possibly like jazz.",
            MemoryType.Preference,
            0.2m,
            0.4m);
        var good = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User prefers Czech.",
            MemoryType.Preference,
            0.8m,
            0.9m);
        store.AddConversation(conversation);
        store.AddMemory(metadata);
        store.AddMemory(lowSignal);
        store.AddMemory(good);

        var preview = await CreateService(store).PreviewAsync("matej");

        Assert.Equal(3, preview.ScannedCount);
        Assert.Contains(preview.Suggestions, suggestion =>
            suggestion.MemoryId == metadata.Id && suggestion.ReasonCode == "technical_metadata");
        Assert.Contains(preview.Suggestions, suggestion =>
            suggestion.MemoryId == lowSignal.Id && suggestion.ReasonCode == "low_signal");
        Assert.DoesNotContain(preview.Suggestions, suggestion => suggestion.MemoryId == good.Id);
    }

    [Fact]
    public async Task Preview_does_not_suggest_explicit_or_pinned_memories()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-1");
        var explicitMetadata = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User ID is matej.",
            MemoryType.Fact,
            0.8m,
            0.9m,
            origin: MemoryOrigin.Explicit);
        var pinnedLowSignal = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "Weak but pinned.",
            MemoryType.Fact,
            0.1m,
            0.1m,
            isPinned: true);
        store.AddConversation(conversation);
        store.AddMemory(explicitMetadata);
        store.AddMemory(pinnedLowSignal);

        var preview = await CreateService(store).PreviewAsync("matej");

        Assert.Empty(preview.Suggestions);
    }

    [Fact]
    public async Task Preview_suggests_near_duplicate_when_embeddings_match()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-1");
        var stronger = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User drinks tea.",
            MemoryType.Preference,
            0.85m,
            0.9m);
        var weaker = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User prefers tea over coffee.",
            MemoryType.Preference,
            0.7m,
            0.8m);
        stronger.SetEmbedding([1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f]);
        weaker.SetEmbedding([1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f]);
        store.AddConversation(conversation);
        store.AddMemory(stronger);
        store.AddMemory(weaker);

        var preview = await CreateService(store).PreviewAsync("matej");

        var duplicate = Assert.Single(preview.Suggestions);
        Assert.Equal(weaker.Id, duplicate.MemoryId);
        Assert.Equal("duplicate", duplicate.ReasonCode);
        Assert.DoesNotContain(preview.Suggestions, suggestion => suggestion.MemoryId == stronger.Id);
    }

    [Fact]
    public async Task Preview_treats_user_prefix_variants_as_exact_duplicates()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-1");
        var first = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User drinks tea.",
            MemoryType.Preference,
            0.8m,
            0.9m);
        var second = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "The user drinks tea",
            MemoryType.Preference,
            0.6m,
            0.7m);
        store.AddConversation(conversation);
        store.AddMemory(first);
        store.AddMemory(second);

        var preview = await CreateService(store).PreviewAsync("matej");

        var duplicate = Assert.Single(preview.Suggestions);
        Assert.Equal(second.Id, duplicate.MemoryId);
        Assert.Equal("duplicate", duplicate.ReasonCode);
    }

    [Fact]
    public async Task Apply_archives_only_current_cleanup_suggestions_for_owner()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-1");
        var foreignConversation = new Conversation("other", "chat-2");
        var metadata = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "Owner id is matej.",
            MemoryType.Fact,
            0.8m,
            0.9m);
        var good = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User prefers Czech.",
            MemoryType.Preference,
            0.8m,
            0.9m);
        var foreignMetadata = new MemoryEntity(
            foreignConversation.OwnerId,
            foreignConversation.Id,
            MemoryScope.User,
            "Owner id is other.",
            MemoryType.Fact,
            0.8m,
            0.9m);
        store.AddConversation(conversation);
        store.AddConversation(foreignConversation);
        store.AddMemory(metadata);
        store.AddMemory(good);
        store.AddMemory(foreignMetadata);

        var result = await CreateService(store).ApplyAsync(
            "matej",
            new MemoryCleanupApplyRequest([metadata.Id, good.Id, foreignMetadata.Id]));

        Assert.Equal(3, result.RequestedCount);
        Assert.Equal(1, result.ArchivedCount);
        Assert.Equal(MemoryStatus.Archived, metadata.Status);
        Assert.Equal(MemoryStatus.Active, good.Status);
        Assert.Equal(MemoryStatus.Active, foreignMetadata.Status);
    }

    private static MemoryCleanupService CreateService(FakeMemoryStore store)
    {
        return new MemoryCleanupService(
            store,
            Options.Create(new MemoryAiOptions
            {
                MinPersistConfidence = 0.55m,
                MinPersistImportance = 0.35m,
                SimilarityThreshold = 0.86f
            }));
    }
}
