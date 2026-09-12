namespace Memory.Application.Memories;

public interface IMemoryCandidateService
{
    Task<IReadOnlyList<MemoryCandidateResponse>> GetPendingForConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryCandidateResponse>> GetPendingForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default);

    Task<MemoryResponse> PromoteAsync(
        Guid candidateId,
        PromoteMemoryCandidateRequest request,
        CancellationToken cancellationToken = default);

    Task<MemoryCandidateResponse> RejectAsync(Guid candidateId, CancellationToken cancellationToken = default);
}
