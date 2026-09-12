namespace Memory.Domain.Conversations;

public sealed class Conversation
{
    private Conversation()
    {
    }

    public Conversation(string ownerId, string externalId, string? title = null, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Conversation owner id is required.", nameof(ownerId));
        }

        if (string.IsNullOrWhiteSpace(externalId))
        {
            throw new ArgumentException("Conversation external id is required.", nameof(externalId));
        }

        var createdAt = now ?? DateTimeOffset.UtcNow;

        Id = Guid.NewGuid();
        OwnerId = ownerId.Trim();
        ExternalId = externalId.Trim();
        Title = NormalizeTitle(title);
        LastMessageSequenceNumber = 0;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; } = string.Empty;
    public string ExternalId { get; private set; } = string.Empty;
    public string? Title { get; private set; }
    public int LastMessageSequenceNumber { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void TryUpdateTitle(string? title, DateTimeOffset? now = null)
    {
        if (title is null)
        {
            return;
        }

        var normalized = NormalizeTitle(title);
        if (Title == normalized)
        {
            return;
        }

        Title = normalized;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    public int ReserveNextMessageSequence(DateTimeOffset? now = null)
    {
        LastMessageSequenceNumber++;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
        return LastMessageSequenceNumber;
    }

    private static string? NormalizeTitle(string? title)
    {
        return string.IsNullOrWhiteSpace(title) ? null : title.Trim();
    }
}
