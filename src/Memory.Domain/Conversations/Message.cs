namespace Memory.Domain.Conversations;

public sealed class Message
{
    private Message()
    {
    }

    public Message(
        Guid conversationId,
        MessageRole role,
        string content,
        int sequenceNumber,
        string? externalId = null,
        DateTimeOffset? occurredAt = null)
    {
        if (conversationId == Guid.Empty)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Message content is required.", nameof(content));
        }

        if (sequenceNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber), "Sequence number cannot be negative.");
        }

        Id = Guid.NewGuid();
        ConversationId = conversationId;
        Role = role;
        Content = content.Trim();
        SequenceNumber = sequenceNumber;
        ExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId.Trim();
        OccurredAt = occurredAt ?? DateTimeOffset.UtcNow;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Conversation Conversation { get; private set; } = null!;
    public string? ExternalId { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public int SequenceNumber { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
