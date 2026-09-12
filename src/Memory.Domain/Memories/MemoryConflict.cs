namespace Memory.Domain.Memories;

public sealed class MemoryConflict
{
    private MemoryConflict()
    {
    }

    public MemoryConflict(
        string ownerId,
        Guid conversationId,
        Guid candidateId,
        Guid conflictingMemoryId,
        decimal confidence,
        string? reason,
        DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(ownerId));
        }

        if (conversationId == Guid.Empty)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (candidateId == Guid.Empty)
        {
            throw new ArgumentException("Candidate id is required.", nameof(candidateId));
        }

        if (conflictingMemoryId == Guid.Empty)
        {
            throw new ArgumentException("Conflicting memory id is required.", nameof(conflictingMemoryId));
        }

        if (candidateId == conflictingMemoryId)
        {
            throw new ArgumentException("Candidate and conflicting memory must be different.", nameof(conflictingMemoryId));
        }

        ValidateScore(confidence, nameof(confidence));

        var createdAt = now ?? DateTimeOffset.UtcNow;
        Id = Guid.NewGuid();
        OwnerId = ownerId.Trim();
        ConversationId = conversationId;
        CandidateId = candidateId;
        ConflictingMemoryId = conflictingMemoryId;
        Confidence = confidence;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        Status = MemoryConflictStatus.Pending;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; } = string.Empty;
    public Guid ConversationId { get; private set; }
    public Guid CandidateId { get; private set; }
    public MemoryCandidate Candidate { get; private set; } = null!;
    public Guid ConflictingMemoryId { get; private set; }
    public Memory ConflictingMemory { get; private set; } = null!;
    public decimal Confidence { get; private set; }
    public string? Reason { get; private set; }
    public MemoryConflictStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void MarkCandidateAccepted(DateTimeOffset? now = null)
    {
        EnsurePending();
        Status = MemoryConflictStatus.CandidateAccepted;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    public void MarkExistingKept(DateTimeOffset? now = null)
    {
        EnsurePending();
        Status = MemoryConflictStatus.ExistingKept;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    private void EnsurePending()
    {
        if (Status != MemoryConflictStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending memory conflict can be resolved.");
        }
    }

    private static void ValidateScore(decimal score, string parameterName)
    {
        if (score is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Score must be between 0 and 1.");
        }
    }
}
