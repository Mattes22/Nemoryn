namespace Memory.Application.Ingestion;

using Memory.Application.Abstractions.Persistence;
using Memory.Application.Exceptions;
using Memory.Domain.Ingestion;

internal sealed class IngestionJobService(IMemoryStore memoryStore) : IIngestionJobService
{
    public async Task<IReadOnlyList<IngestionJobResponse>> GetForConversationAsync(
        Guid conversationId,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        var exists = await memoryStore.ConversationExistsAsync(conversationId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Conversation was not found.");
        }

        var jobs = await memoryStore.GetIngestionJobsForConversationAsync(
            conversationId,
            NormalizeLimit(take),
            cancellationToken);

        return jobs.Select(ToResponse).ToArray();
    }

    public async Task<IReadOnlyList<IngestionJobResponse>> GetForOwnerAsync(
        string ownerId,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(ownerId));
        }

        var jobs = await memoryStore.GetIngestionJobsForOwnerAsync(
            ownerId.Trim(),
            NormalizeLimit(take),
            cancellationToken);

        return jobs.Select(ToResponse).ToArray();
    }

    private static IngestionJobResponse ToResponse(MemoryIngestionJob job)
    {
        return new IngestionJobResponse(
            job.Id,
            job.ConversationId,
            job.MessageId,
            job.Status,
            job.AttemptCount,
            job.LockedUntil,
            job.LastError,
            job.CreatedAt,
            job.UpdatedAt);
    }

    private static int NormalizeLimit(int? take)
    {
        return take is > 0 ? Math.Min(take.Value, 200) : 50;
    }
}
