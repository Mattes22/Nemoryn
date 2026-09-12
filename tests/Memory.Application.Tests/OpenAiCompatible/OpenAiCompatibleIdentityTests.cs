namespace Memory.Application.Tests.OpenAiCompatible;

using Memory.Application.OpenAiCompatible;

public sealed class OpenAiCompatibleIdentityTests
{
    [Fact]
    public void Owner_prefers_open_webui_user_id()
    {
        var ownerId = OpenAiCompatibleIdentity.ResolveOwnerId(
            "user-42",
            "matej@example.com",
            "Matej",
            "metadata-owner",
            "body-user");

        Assert.Equal("user-42", ownerId);
    }

    [Fact]
    public void Owner_falls_back_to_anonymous()
    {
        Assert.Equal("anonymous", OpenAiCompatibleIdentity.ResolveOwnerId(null, "  ", string.Empty));
    }

    [Fact]
    public void Conversation_prefers_open_webui_chat_id()
    {
        var key = OpenAiCompatibleIdentity.ResolveConversationKey(
            "matej",
            "owui-chat-1",
            "metadata-chat",
            "body-chat");

        Assert.Equal("owui-chat-1", key);
    }

    [Fact]
    public void Conversation_falls_back_to_owner_default()
    {
        Assert.Equal("matej-default", OpenAiCompatibleIdentity.ResolveConversationKey("matej"));
    }
}
