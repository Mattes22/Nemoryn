namespace Memory.Application.Conversations;

using Memory.Domain.Conversations;

public sealed record AddMessageRequest(
    MessageRole Role,
    string Content,
    string? ExternalId,
    DateTimeOffset? OccurredAt,
    bool EnqueueIngestion = true);
