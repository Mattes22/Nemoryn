namespace Memory.Application.OpenAiCompatible;

public sealed record OpenAiCompatibleChatCommand(
    string OwnerId,
    string ConversationKey,
    string UserMessage,
    int? MemoryLimit,
    int? RecentMessageCount,
    string? ChatModel = null);
