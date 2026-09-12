namespace Memory.Application.Ingestion;

using Memory.Domain.Ingestion;

public sealed record IngestionJobResponse(
    Guid Id,
    Guid ConversationId,
    Guid MessageId,
    IngestionJobStatus Status,
    int AttemptCount,
    DateTimeOffset? LockedUntil,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
