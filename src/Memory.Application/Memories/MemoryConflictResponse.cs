namespace Memory.Application.Memories;

using Memory.Domain.Memories;

public sealed record MemoryConflictResponse(
    Guid Id,
    string OwnerId,
    Guid ConversationId,
    MemoryConflictStatus Status,
    decimal Confidence,
    string? Reason,
    MemoryCandidateResponse Candidate,
    MemoryResponse ConflictingMemory,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
