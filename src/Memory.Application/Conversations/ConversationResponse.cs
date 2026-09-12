namespace Memory.Application.Conversations;

public sealed record ConversationResponse(
    Guid Id,
    string OwnerId,
    string ExternalId,
    string? Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
