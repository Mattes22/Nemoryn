namespace Memory.Application.Conversations;

using Memory.Domain.Conversations;

public sealed record ConversationMessageResponse(
    Guid Id,
    Guid ConversationId,
    string? ExternalId,
    MessageRole Role,
    string Content,
    int SequenceNumber,
    DateTimeOffset OccurredAt,
    DateTimeOffset CreatedAt);
