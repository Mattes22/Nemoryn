namespace Memory.Domain.Memories;

using ConversationEntity = global::Memory.Domain.Conversations.Conversation;
using MessageEntity = global::Memory.Domain.Conversations.Message;

public sealed class MemoryEvidence
{
    private MemoryEvidence()
    {
    }

    public MemoryEvidence(
        Guid candidateId,
        Guid conversationId,
        Guid sourceMessageId,
        string? sourceSummary,
        DateTimeOffset? now = null)
    {
        if (candidateId == Guid.Empty)
        {
            throw new ArgumentException("Candidate id is required.", nameof(candidateId));
        }

        if (conversationId == Guid.Empty)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (sourceMessageId == Guid.Empty)
        {
            throw new ArgumentException("Source message id is required.", nameof(sourceMessageId));
        }

        Id = Guid.NewGuid();
        CandidateId = candidateId;
        ConversationId = conversationId;
        SourceMessageId = sourceMessageId;
        SourceSummary = string.IsNullOrWhiteSpace(sourceSummary) ? null : sourceSummary.Trim();
        CreatedAt = now ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid CandidateId { get; private set; }
    public MemoryCandidate Candidate { get; private set; } = null!;
    public Guid ConversationId { get; private set; }
    public ConversationEntity Conversation { get; private set; } = null!;
    public Guid SourceMessageId { get; private set; }
    public MessageEntity SourceMessage { get; private set; } = null!;
    public string? SourceSummary { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
