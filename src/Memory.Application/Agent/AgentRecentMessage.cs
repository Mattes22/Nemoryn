namespace Memory.Application.Agent;

using Memory.Domain.Conversations;

public sealed record AgentRecentMessage(
    Guid Id,
    MessageRole Role,
    string Content,
    int SequenceNumber,
    DateTimeOffset OccurredAt);
