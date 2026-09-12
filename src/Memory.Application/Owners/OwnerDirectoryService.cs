namespace Memory.Application.Owners;

using Memory.Application.Abstractions.Persistence;

internal sealed class OwnerDirectoryService(IMemoryStore memoryStore) : IOwnerDirectoryService
{
    public Task<IReadOnlyList<OwnerSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        return memoryStore.ListOwnersAsync(cancellationToken);
    }
}
