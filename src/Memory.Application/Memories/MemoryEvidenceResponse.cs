namespace Memory.Application.Memories;

public sealed record MemoryEvidenceResponse(
    Guid Id,
    Guid CandidateId,
    Guid ConversationId,
    Guid SourceMessageId,
    string? SourceSummary,
    DateTimeOffset CreatedAt);
