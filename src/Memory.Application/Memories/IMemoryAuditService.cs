namespace Memory.Application.Memories;

public interface IMemoryAuditService
{
    Task<IReadOnlyList<MemoryAuditLogResponse>> GetForOwnerAsync(
        string ownerId,
        Guid? memoryId = null,
        Guid? conversationId = null,
        int take = 50,
        CancellationToken cancellationToken = default);
}
