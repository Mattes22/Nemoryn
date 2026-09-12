namespace Memory.Application.Conversations;

public sealed record CreateConversationRequest(string OwnerId, string ExternalId, string? Title);
