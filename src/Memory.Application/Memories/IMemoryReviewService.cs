namespace Memory.Application.Memories;

public interface IMemoryReviewService
{
    Task<MemoryReviewResponse> GetAsync(
        string ownerId,
        Guid? conversationId = null,
        CancellationToken cancellationToken = default);
}
