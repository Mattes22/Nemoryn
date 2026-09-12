namespace Memory.Application.Context;

using Memory.Domain.Memories;

public sealed record MemoryContextItem(
    Guid Id,
    string Content,
    MemoryType Type,
    MemoryScope Scope,
    MemoryOrigin Origin,
    bool IsPinned,
    decimal Importance,
    decimal Confidence,
    double? Similarity,
    double Score,
    string SelectionKind,
    string SelectionReason,
    string? SourceSummary,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil);
