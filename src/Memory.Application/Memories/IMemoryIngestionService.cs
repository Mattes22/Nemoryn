namespace Memory.Application.Memories;

public interface IMemoryIngestionService
{
    Task<bool> IngestFromMessageAsync(
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken = default);

    Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default);
}
