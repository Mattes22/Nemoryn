namespace Memory.Application.Memories;

using Memory.Domain.Memories;

public sealed record CreateMemoryRequest(
    Guid ConversationId,
    string Content,
    MemoryType Type,
    decimal Importance,
    decimal Confidence,
    Guid? SourceMessageId,
    string? SourceSummary,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    string? SourceMetadataJson,
    MemoryScope? Scope = null,
    bool Pin = false);
