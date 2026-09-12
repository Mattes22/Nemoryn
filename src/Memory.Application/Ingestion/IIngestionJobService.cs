namespace Memory.Application.Ingestion;

public interface IIngestionJobService
{
    Task<IReadOnlyList<IngestionJobResponse>> GetForConversationAsync(
        Guid conversationId,
        int? take = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IngestionJobResponse>> GetForOwnerAsync(
        string ownerId,
        int? take = null,
        CancellationToken cancellationToken = default);
}
