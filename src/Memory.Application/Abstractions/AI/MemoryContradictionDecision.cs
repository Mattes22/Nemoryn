namespace Memory.Application.Abstractions.AI;

public sealed record MemoryContradictionDecision(
    MemoryContradictionRelation Relation,
    Guid? ConflictingMemoryId,
    decimal Confidence,
    string? Reason);
