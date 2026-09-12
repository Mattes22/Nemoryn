namespace Memory.Domain.Ingestion;

public sealed class MemoryIngestionJob
{
    public const int MaxAttempts = 3;

    private MemoryIngestionJob()
    {
    }

    public MemoryIngestionJob(Guid conversationId, Guid messageId, DateTimeOffset? now = null)
    {
        if (conversationId == Guid.Empty)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (messageId == Guid.Empty)
        {
            throw new ArgumentException("Message id is required.", nameof(messageId));
        }

        var createdAt = now ?? DateTimeOffset.UtcNow;

        Id = Guid.NewGuid();
        ConversationId = conversationId;
        MessageId = messageId;
        Status = IngestionJobStatus.Pending;
        AttemptCount = 0;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid MessageId { get; private set; }
    public IngestionJobStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void MarkProcessing(DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        Status = IngestionJobStatus.Processing;
        AttemptCount++;
        LockedUntil = at.AddMinutes(2);
        UpdatedAt = at;
    }

    public void MarkSucceeded(DateTimeOffset? now = null)
    {
        Status = IngestionJobStatus.Succeeded;
        LockedUntil = null;
        LastError = null;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    public void MarkSkipped(string reason, DateTimeOffset? now = null)
    {
        Status = IngestionJobStatus.Skipped;
        LockedUntil = null;
        LastError = TrimError(reason);
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string error, DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        LastError = TrimError(error);
        LockedUntil = null;
        UpdatedAt = at;
        Status = AttemptCount >= MaxAttempts
            ? IngestionJobStatus.Failed
            : IngestionJobStatus.Pending;
    }

    private static string TrimError(string error)
    {
        var trimmed = error.Trim();
        return trimmed.Length <= 2000 ? trimmed : trimmed[..2000];
    }
}
