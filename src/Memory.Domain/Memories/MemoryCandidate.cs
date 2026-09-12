namespace Memory.Domain.Memories;

public sealed class MemoryCandidate
{
    private MemoryCandidate()
    {
    }

    public MemoryCandidate(
        string ownerId,
        Guid conversationId,
        MemoryScope scope,
        string content,
        MemoryType type,
        decimal importance,
        decimal confidence,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validUntil = null,
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

        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), "Unknown memory scope.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), "Unknown memory type.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Memory candidate content is required.", nameof(content));
        }

        ValidateScore(importance, nameof(importance));
        ValidateScore(confidence, nameof(confidence));

        if (validFrom is not null && validUntil is not null && validUntil <= validFrom)
        {
            throw new ArgumentException("Valid until must be after valid from.", nameof(validUntil));
        }

        var createdAt = now ?? DateTimeOffset.UtcNow;
        var normalizedContent = content.Trim();

        Id = Guid.NewGuid();
        OwnerId = ownerId.Trim();
        ConversationId = conversationId;
        Scope = scope;
        Content = normalizedContent;
        Type = type;
        Status = MemoryCandidateStatus.Pending;
        Importance = importance;
        Confidence = confidence;
        Fingerprint = MemoryFingerprint.Compute(OwnerId, Scope, Type, normalizedContent);
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        EvidenceCount = 0;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; } = string.Empty;
    public Guid ConversationId { get; private set; }
    public MemoryScope Scope { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public MemoryType Type { get; private set; }
    public MemoryCandidateStatus Status { get; private set; }
    public string Fingerprint { get; private set; } = string.Empty;
    public decimal Importance { get; private set; }
    public decimal Confidence { get; private set; }
    public DateTimeOffset? ValidFrom { get; private set; }
    public DateTimeOffset? ValidUntil { get; private set; }
    public int EvidenceCount { get; private set; }
    public DateTimeOffset? LastEvidenceAt { get; private set; }
    public Guid? PromotedMemoryId { get; private set; }
    public Guid? MergedIntoCandidateId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void AddEvidence(decimal importance, decimal confidence, DateTimeOffset? now = null)
    {
        EnsurePending();
        ValidateScore(importance, nameof(importance));
        ValidateScore(confidence, nameof(confidence));

        var at = now ?? DateTimeOffset.UtcNow;
        EvidenceCount++;
        Importance = Math.Max(Importance, importance);
        Confidence = Math.Max(Confidence, confidence);
        LastEvidenceAt = at;
        UpdatedAt = at;
    }

    public void Promote(Guid memoryId, DateTimeOffset? now = null)
    {
        EnsurePending();
        if (memoryId == Guid.Empty)
        {
            throw new ArgumentException("Promoted memory id is required.", nameof(memoryId));
        }

        Status = MemoryCandidateStatus.Promoted;
        PromotedMemoryId = memoryId;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    public void Reject(DateTimeOffset? now = null)
    {
        EnsurePending();
        Status = MemoryCandidateStatus.Rejected;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    public void MergeInto(Guid candidateId, DateTimeOffset? now = null)
    {
        EnsurePending();
        if (candidateId == Guid.Empty)
        {
            throw new ArgumentException("Target candidate id is required.", nameof(candidateId));
        }

        if (candidateId == Id)
        {
            throw new ArgumentException("A candidate cannot be merged into itself.", nameof(candidateId));
        }

        Status = MemoryCandidateStatus.Merged;
        MergedIntoCandidateId = candidateId;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    private void EnsurePending()
    {
        if (Status != MemoryCandidateStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending memory candidate can be changed.");
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
