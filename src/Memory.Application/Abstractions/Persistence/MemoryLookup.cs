namespace Memory.Application.Abstractions.Persistence;

public sealed record MemoryLookup(
    string OwnerId,
    Guid? ConversationId,
    bool IncludeUserScope,
    bool IncludeConversationScope)
{
    public static MemoryLookup ForConversation(string ownerId, Guid conversationId)
    {
        return new(ownerId, conversationId, IncludeUserScope: false, IncludeConversationScope: true);
    }

    public static MemoryLookup ForUser(string ownerId)
    {
        return new(ownerId, ConversationId: null, IncludeUserScope: true, IncludeConversationScope: false);
    }

    public static MemoryLookup ForRetrieval(string ownerId, Guid conversationId)
    {
        return new(ownerId, conversationId, IncludeUserScope: true, IncludeConversationScope: true);
    }
}
