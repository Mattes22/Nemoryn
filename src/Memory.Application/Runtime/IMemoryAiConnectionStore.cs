namespace Memory.Application.Runtime;

public sealed record MemoryAiConnectionSettings(
    string BaseUrl,
    string ChatModel,
    string EmbeddingModel);

public interface IMemoryAiConnectionStore
{
    MemoryAiConnectionSettings? Load();
    bool TrySave(MemoryAiConnectionSettings settings, out string? error);
}
