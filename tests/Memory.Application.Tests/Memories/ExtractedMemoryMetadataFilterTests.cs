namespace Memory.Application.Tests.Memories;

using Memory.Application.Memories;

public sealed class ExtractedMemoryMetadataFilterTests
{
    private static readonly Guid ConversationId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Theory]
    [InlineData("Owner id is user-1.")]
    [InlineData("The ownerId of this account is user-1")]
    [InlineData("user-1")]
    [InlineData("id: user-1")]
    [InlineData("Conversation id is cccccccc-cccc-cccc-cccc-cccccccccccc")]
    [InlineData("User's name is user-1.")]
    public void Discards_technical_identifiers(string content)
    {
        Assert.True(ExtractedMemoryMetadataFilter.ShouldDiscard(content, "user-1", ConversationId, "chat-9"));
    }

    [Theory]
    [InlineData("User likes tea.")]
    [InlineData("User named user-1 likes tea.")]
    [InlineData("The user lives in Prague.")]
    [InlineData("User's name is Matej.")]
    public void Keeps_biographical_facts(string content)
    {
        Assert.False(ExtractedMemoryMetadataFilter.ShouldDiscard(content, "user-1", ConversationId, "chat-9"));
    }
}
