namespace Memory.Domain.Memories;

public sealed class MemoryAuditLog
{
    private MemoryAuditLog()
    {
    }

    public MemoryAuditLog(
        string ownerId,
        MemoryAuditAction action,
        MemoryAuditActorKind actorKind,
        Guid? conversationId = null,
        Guid? memoryId = null,
        Guid? candidateId = null,
        Guid? conflictId = null,
        string? reason = null,
        string? details = null,
        DateTimeOffset? occurredAt = null)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(ownerId));
        }

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), "Unknown memory audit action.");
        }

        if (!Enum.IsDefined(actorKind))
        {
            throw new ArgumentOutOfRangeException(nameof(actorKind), "Unknown memory audit actor.");
        }

        Id = Guid.NewGuid();
        OwnerId = ownerId.Trim();
        ConversationId = conversationId == Guid.Empty ? null : conversationId;
        MemoryId = memoryId == Guid.Empty ? null : memoryId;
        CandidateId = candidateId == Guid.Empty ? null : candidateId;
        ConflictId = conflictId == Guid.Empty ? null : conflictId;
        Action = action;
        ActorKind = actorKind;
        ActorId = ActorIdFor(actorKind, OwnerId);
        Reason = Truncate(reason, 500);
        Details = Truncate(details, 2000);
        OccurredAt = occurredAt ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; } = string.Empty;
    public Guid? ConversationId { get; private set; }
    public Guid? MemoryId { get; private set; }
    public Guid? CandidateId { get; private set; }
    public Guid? ConflictId { get; private set; }
    public MemoryAuditAction Action { get; private set; }
    public MemoryAuditActorKind ActorKind { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public string? Details { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    public static MemoryAuditLog ForMemory(
        MemoryAuditAction action,
        Memory memory,
        MemoryAuditActorKind actorKind,
        string? reason = null,
        string? details = null,
        Guid? candidateId = null,
        Guid? conflictId = null)
    {
        ArgumentNullException.ThrowIfNull(memory);

        return new MemoryAuditLog(
            memory.OwnerId,
            action,
            actorKind,
            memory.ConversationId,
            memory.Id,
            candidateId,
            conflictId,
            reason,
            details ?? memory.Content);
    }

    public static MemoryAuditLog ForCandidate(
        MemoryAuditAction action,
        MemoryCandidate candidate,
        MemoryAuditActorKind actorKind,
        string? reason = null,
        Guid? memoryId = null,
        Guid? conflictId = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return new MemoryAuditLog(
            candidate.OwnerId,
            action,
            actorKind,
            candidate.ConversationId,
            memoryId,
            candidate.Id,
            conflictId,
            reason,
            candidate.Content);
    }

    public static MemoryAuditLog ForConflict(
        MemoryAuditAction action,
        MemoryConflict conflict,
        MemoryAuditActorKind actorKind,
        string? reason = null,
        Guid? memoryId = null)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        return new MemoryAuditLog(
            conflict.OwnerId,
            action,
            actorKind,
            conflict.ConversationId,
            memoryId ?? conflict.ConflictingMemoryId,
            conflict.CandidateId,
            conflict.Id,
            reason ?? conflict.Reason);
    }

    private static string ActorIdFor(MemoryAuditActorKind actorKind, string ownerId)
    {
        return actorKind switch
        {
            MemoryAuditActorKind.Ingestion => "ingestion",
            MemoryAuditActorKind.Cleanup => "cleanup",
            MemoryAuditActorKind.Retention => "retention",
            _ => ownerId
        };
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
