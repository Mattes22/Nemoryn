namespace Memory.Application.Memories;

using Memory.Application.Abstractions.Persistence;
using Memory.Domain.Memories;

internal sealed class MemoryAuditService(IMemoryStore memoryStore) : IMemoryAuditService
{
    public async Task<IReadOnlyList<MemoryAuditLogResponse>> GetForOwnerAsync(
        string ownerId,
        Guid? memoryId = null,
        Guid? conversationId = null,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(ownerId));
        }

        var entries = await memoryStore.GetMemoryAuditLogsAsync(
            ownerId.Trim(),
            memoryId,
            conversationId,
            take,
            cancellationToken);

        return entries.Select(ToResponse).ToArray();
    }

    internal static MemoryAuditLogResponse ToResponse(MemoryAuditLog entry)
    {
        return new MemoryAuditLogResponse(
            entry.Id,
            entry.OwnerId,
            entry.ConversationId,
            entry.MemoryId,
            entry.CandidateId,
            entry.ConflictId,
            entry.Action,
            entry.ActorKind,
            entry.ActorId,
            entry.Reason,
            entry.Details,
            entry.OccurredAt);
    }
}
