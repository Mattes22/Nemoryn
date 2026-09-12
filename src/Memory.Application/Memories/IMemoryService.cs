namespace Memory.Application.Memories;

public interface IMemoryService
{
    Task<MemoryResponse> CreateAsync(CreateMemoryRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryResponse>> GetActiveForConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryResponse>> GetActiveForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemorySearchMatch>> SearchAsync(
        Guid conversationId,
        SearchMemoriesRequest request,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RankedMemory>> RetrieveAsync(
        Guid conversationId,
        SearchMemoriesRequest request,
        CancellationToken cancellationToken = default);
    Task<StableMemorySet> RetrieveStableAsync(
        Guid conversationId,
        SearchMemoriesRequest request,
        CancellationToken cancellationToken = default);
    Task<MemoryResponse> PinAsync(Guid memoryId, CancellationToken cancellationToken = default);
    Task<MemoryResponse> UnpinAsync(Guid memoryId, CancellationToken cancellationToken = default);
    Task<MemoryResponse> ForgetAsync(Guid memoryId, CancellationToken cancellationToken = default);
    Task<MemoryResponse> CorrectAsync(
        Guid memoryId,
        CorrectMemoryRequest request,
        CancellationToken cancellationToken = default);
}
