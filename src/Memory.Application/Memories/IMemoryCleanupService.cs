namespace Memory.Application.Memories;

public interface IMemoryCleanupService
{
    Task<MemoryCleanupPreviewResponse> PreviewAsync(
        string ownerId,
        CancellationToken cancellationToken = default);

    Task<MemoryCleanupApplyResponse> ApplyAsync(
        string ownerId,
        MemoryCleanupApplyRequest request,
        CancellationToken cancellationToken = default);
}
