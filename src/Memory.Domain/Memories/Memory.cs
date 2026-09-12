namespace Memory.Domain.Memories;

using ConversationEntity = global::Memory.Domain.Conversations.Conversation;
using MessageEntity = global::Memory.Domain.Conversations.Message;

public sealed class Memory
{
    private Memory()
    {
    }

    public Memory(
        string ownerId,
        Guid conversationId,
        MemoryScope scope,
        string content,
        MemoryType type,
        decimal importance,
        decimal confidence,
        Guid? sourceMessageId = null,
        string? sourceSummary = null,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validUntil = null,
        MemoryOrigin origin = MemoryOrigin.Inferred,
        bool isPinned = false)
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

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Memory content is required.", nameof(content));
        }

        ValidateScore(importance, nameof(importance));
        ValidateScore(confidence, nameof(confidence));

        if (validFrom is not null && validUntil is not null && validUntil <= validFrom)
        {
            throw new ArgumentException("Valid until must be after valid from.", nameof(validUntil));
        }

        if (!Enum.IsDefined(origin))
        {
            throw new ArgumentOutOfRangeException(nameof(origin), "Unknown memory origin.");
        }

        var trimmedContent = content.Trim();
        var createdAt = DateTimeOffset.UtcNow;

        Id = Guid.NewGuid();
        OwnerId = ownerId.Trim();
        ConversationId = conversationId;
        Scope = scope;
        Content = trimmedContent;
        Type = type;
        Status = MemoryStatus.Active;
        Origin = origin;
        IsPinned = isPinned;
        Importance = importance;
        Confidence = confidence;
        Fingerprint = MemoryFingerprint.Compute(OwnerId, Scope, Type, trimmedContent);
        SourceMessageId = sourceMessageId;
        SourceSummary = string.IsNullOrWhiteSpace(sourceSummary) ? null : sourceSummary.Trim();
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        Embedding = null;
    }

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; } = string.Empty;
    public Guid ConversationId { get; private set; }
    public ConversationEntity Conversation { get; private set; } = null!;
    public MemoryScope Scope { get; private set; }
    public Guid? SourceMessageId { get; private set; }
    public MessageEntity? SourceMessage { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public MemoryType Type { get; private set; }
    public MemoryStatus Status { get; private set; }
    public MemoryOrigin Origin { get; private set; }
    public bool IsPinned { get; private set; }
    public string Fingerprint { get; private set; } = string.Empty;
    public decimal Importance { get; private set; }
    public decimal Confidence { get; private set; }
    public DateTimeOffset? ValidFrom { get; private set; }
    public DateTimeOffset? ValidUntil { get; private set; }
    public string? SourceSummary { get; private set; }
    public string? SourceMetadataJson { get; private set; }
    public Guid? SupersededByMemoryId { get; private set; }
    public Memory? SupersededByMemory { get; private set; }
    public float[]? Embedding { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsEffective(DateTimeOffset at)
    {
        if (Status != MemoryStatus.Active)
        {
            return false;
        }

        if (ValidFrom is not null && ValidFrom > at)
        {
            return false;
        }

        return ValidUntil is null || ValidUntil > at;
    }

    public bool CanBeSupersededBy(Memory replacement, decimal confidenceMargin = MemoryStability.DefaultSupersedeConfidenceMargin)
    {
        if (!IsEffective(DateTimeOffset.UtcNow))
        {
            return false;
        }

        if (IsPinned)
        {
            return false;
        }

        if (Origin == MemoryOrigin.Explicit && replacement.Origin != MemoryOrigin.Explicit)
        {
            return false;
        }

        return replacement.Confidence + confidenceMargin >= Confidence;
    }

    public void Pin()
    {
        EnsureActive();
        IsPinned = true;
        Importance = Math.Max(Importance, 0.85m);
        Origin = MemoryOrigin.Explicit;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Unpin()
    {
        EnsureActive();
        IsPinned = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Correct(string content)
    {
        EnsureActive();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Memory content is required.", nameof(content));
        }

        Content = content.Trim();
        Fingerprint = MemoryFingerprint.Compute(OwnerId, Scope, Type, Content);
        Origin = MemoryOrigin.Explicit;
        Confidence = Math.Max(Confidence, 0.95m);
        Importance = Math.Max(Importance, 0.75m);
        Embedding = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Forget()
    {
        EnsureActive();
        IsPinned = false;
        Archive();
    }

    public void SetEmbedding(IReadOnlyList<float> embedding)
    {
        ArgumentNullException.ThrowIfNull(embedding);

        if (embedding.Count == 0)
        {
            throw new ArgumentException("Embedding cannot be empty.", nameof(embedding));
        }

        Embedding = embedding.ToArray();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetSourceMetadataJson(string? sourceMetadataJson)
    {
        SourceMetadataJson = string.IsNullOrWhiteSpace(sourceMetadataJson) ? null : sourceMetadataJson.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Supersede(Guid replacementMemoryId)
    {
        if (Status != MemoryStatus.Active)
        {
            throw new InvalidOperationException("Only an active memory can be superseded.");
        }

        if (replacementMemoryId == Guid.Empty)
        {
            throw new ArgumentException("Replacement memory id is required.", nameof(replacementMemoryId));
        }

        if (replacementMemoryId == Id)
        {
            throw new ArgumentException("A memory cannot supersede itself.", nameof(replacementMemoryId));
        }

        Status = MemoryStatus.Superseded;
        IsPinned = false;
        SupersededByMemoryId = replacementMemoryId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Archive()
    {
        Status = MemoryStatus.Archived;
        IsPinned = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reject()
    {
        Status = MemoryStatus.Rejected;
        IsPinned = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureActive()
    {
        if (Status != MemoryStatus.Active)
        {
            throw new InvalidOperationException("Only an active memory can be changed.");
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
