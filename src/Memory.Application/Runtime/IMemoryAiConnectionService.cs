namespace Memory.Application.Runtime;

public sealed record MemoryAiConnectionResponse(
    string Provider,
    string BaseUrl,
    string ChatModel,
    string EmbeddingModel,
    IReadOnlyList<string> ChatModels,
    string? ModelsError,
    bool Persisted,
    string? PersistError);

public sealed record MemoryAiConnectionRequest(
    string BaseUrl,
    string ChatModel,
    string EmbeddingModel);

public interface IMemoryAiConnectionService
{
    Task<MemoryAiConnectionResponse> GetAsync(CancellationToken cancellationToken = default);
    Task<MemoryAiConnectionResponse> SetAsync(
        MemoryAiConnectionRequest request,
        CancellationToken cancellationToken = default);
}
