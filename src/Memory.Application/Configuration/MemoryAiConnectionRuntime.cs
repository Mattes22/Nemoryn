namespace Memory.Application.Configuration;

using Memory.Application.Runtime;

public sealed class MemoryAiConnectionRuntime
{
    public string? BaseUrl { get; set; }
    public string? ChatModel { get; set; }
    public string? EmbeddingModel { get; set; }

    public void ApplyTo(MemoryAiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            options.BaseUrl = BaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(ChatModel))
        {
            options.ChatModel = ChatModel;
        }

        if (!string.IsNullOrWhiteSpace(EmbeddingModel))
        {
            options.EmbeddingModel = EmbeddingModel;
        }
    }

    public static MemoryAiConnectionRuntime FromStore(IMemoryAiConnectionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        var runtime = new MemoryAiConnectionRuntime();
        var loaded = store.Load();
        if (loaded is null)
        {
            return runtime;
        }

        runtime.BaseUrl = loaded.BaseUrl;
        runtime.ChatModel = loaded.ChatModel;
        runtime.EmbeddingModel = loaded.EmbeddingModel;
        return runtime;
    }
}
