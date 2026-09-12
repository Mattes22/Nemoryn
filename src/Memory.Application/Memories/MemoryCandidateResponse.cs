namespace Memory.Application.Memories;

using Memory.Domain.Memories;

public sealed record MemoryCandidateResponse(
    Guid Id,
    string OwnerId,
    Guid ConversationId,
    MemoryScope Scope,
    string Content,
    MemoryType Type,
    MemoryCandidateStatus Status,
    decimal Importance,
    decimal Confidence,
    int EvidenceCount,
    DateTimeOffset? LastEvidenceAt,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    Guid? PromotedMemoryId,
    Guid? MergedIntoCandidateId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<MemoryEvidenceResponse> Evidence);
