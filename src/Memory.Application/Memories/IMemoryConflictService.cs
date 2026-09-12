namespace Memory.Application.Memories;

public interface IMemoryConflictService
{
    Task<IReadOnlyList<MemoryConflictResponse>> GetPendingForConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryConflictResponse>> GetPendingForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default);

    Task<MemoryResponse> AcceptCandidateAsync(Guid conflictId, CancellationToken cancellationToken = default);

    Task<MemoryConflictResponse> KeepExistingAsync(Guid conflictId, CancellationToken cancellationToken = default);
}
