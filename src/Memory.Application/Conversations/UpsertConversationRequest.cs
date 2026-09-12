namespace Memory.Application.Conversations;

public sealed record UpsertConversationRequest(string OwnerId, string? Title);
