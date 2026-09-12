namespace Memory.Application.Conversations;

using Memory.Domain.Conversations;
using Memory.Domain.Ingestion;

public sealed record MessageResponse(
    Guid Id,
    Guid ConversationId,
    string? ExternalId,
    MessageRole Role,
    string Content,
    int SequenceNumber,
    DateTimeOffset OccurredAt,
    DateTimeOffset CreatedAt,
    Guid? IngestionJobId,
    IngestionJobStatus? IngestionStatus);
