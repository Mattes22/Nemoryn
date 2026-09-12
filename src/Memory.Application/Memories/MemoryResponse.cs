namespace Memory.Application.Memories;

using Memory.Domain.Memories;

public sealed record MemoryResponse(
    Guid Id,
    string OwnerId,
    Guid ConversationId,
    MemoryScope Scope,
    Guid? SourceMessageId,
    string Content,
    MemoryType Type,
    MemoryStatus Status,
    MemoryOrigin Origin,
    bool IsPinned,
    decimal Importance,
    decimal Confidence,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    string? SourceSummary,
    string? SourceMetadataJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
