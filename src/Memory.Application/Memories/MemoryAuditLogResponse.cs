namespace Memory.Application.Memories;

using Memory.Domain.Memories;

public sealed record MemoryAuditLogResponse(
    Guid Id,
    string OwnerId,
    Guid? ConversationId,
    Guid? MemoryId,
    Guid? CandidateId,
    Guid? ConflictId,
    MemoryAuditAction Action,
    MemoryAuditActorKind ActorKind,
    string ActorId,
    string? Reason,
    string? Details,
    DateTimeOffset OccurredAt);
