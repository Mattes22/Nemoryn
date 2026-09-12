namespace Memory.Application.Abstractions.AI;

using Memory.Domain.Memories;

public sealed record ExtractedMemoryCandidate(
    string Content,
    MemoryType Type,
    MemoryScope Scope,
    decimal Importance,
    decimal Confidence,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    string? SourceSummary);
