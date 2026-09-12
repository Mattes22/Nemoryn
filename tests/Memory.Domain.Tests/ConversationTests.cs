namespace Memory.Domain.Tests.Conversations;

using Memory.Domain.Conversations;

public sealed class ConversationTests
{
    [Fact]
    public void Constructor_requires_owner_and_external_id()
    {
        Assert.Throws<ArgumentException>(() => new Conversation("  ", "chat-1"));
        Assert.Throws<ArgumentException>(() => new Conversation("user-1", "  "));
    }

    [Fact]
    public void Constructor_trims_values()
    {
        var conversation = new Conversation("  user-1  ", "  chat-1  ", "  Hello  ");

        Assert.Equal("user-1", conversation.OwnerId);
        Assert.Equal("chat-1", conversation.ExternalId);
        Assert.Equal("Hello", conversation.Title);
        Assert.Equal(0, conversation.LastMessageSequenceNumber);
    }

    [Fact]
    public void TryUpdateTitle_ignores_null()
    {
        var conversation = new Conversation("user-1", "chat-1", "Original");
        var updatedAt = conversation.UpdatedAt;

        conversation.TryUpdateTitle(null, now: updatedAt.AddMinutes(1));

        Assert.Equal("Original", conversation.Title);
        Assert.Equal(updatedAt, conversation.UpdatedAt);
    }

    [Fact]
    public void TryUpdateTitle_clears_on_whitespace()
    {
        var conversation = new Conversation("user-1", "chat-1", "Original");

        conversation.TryUpdateTitle("   ");

        Assert.Null(conversation.Title);
    }

    [Fact]
    public void ReserveNextMessageSequence_increments_from_zero()
    {
        var conversation = new Conversation("user-1", "chat-1");

        var first = conversation.ReserveNextMessageSequence();
        var second = conversation.ReserveNextMessageSequence();

        Assert.Equal(1, first);
        Assert.Equal(2, second);
        Assert.Equal(2, conversation.LastMessageSequenceNumber);
    }
}
