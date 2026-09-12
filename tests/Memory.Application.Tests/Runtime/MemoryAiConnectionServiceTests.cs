namespace Memory.Application.Tests.Runtime;

using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.AI;
using Memory.Application.Configuration;
using Memory.Application.Runtime;
using Memory.Application.Tests.Fakes;

public sealed class MemoryAiConnectionServiceTests
{
    [Fact]
    public async Task Set_normalizes_base_url_and_refreshes_models()
    {
        var runtime = new MemoryAiConnectionRuntime();
        var catalog = new FakeAiModelCatalog { Models = ["gpt-oss:20b", "nomic-embed-text"] };
        var store = new FakeMemoryAiConnectionStore();
        var service = CreateService(runtime, catalog, store);

        var response = await service.SetAsync(new MemoryAiConnectionRequest(
            "http://192.168.1.2:11434/",
            "gpt-oss:20b",
            "nomic-embed-text"));

        Assert.Equal("http://192.168.1.2:11434", response.BaseUrl);
        Assert.Equal("http://192.168.1.2:11434", runtime.BaseUrl);
        Assert.Equal("gpt-oss:20b", response.ChatModel);
        Assert.Equal("nomic-embed-text", response.EmbeddingModel);
        Assert.Equal(["gpt-oss:20b"], response.ChatModels);
        Assert.True(response.Persisted);
        Assert.Null(response.PersistError);
        Assert.Equal("http://192.168.1.2:11434", store.Settings?.BaseUrl);
        Assert.Equal("gpt-oss:20b", store.Settings?.ChatModel);
    }

    [Fact]
    public async Task Set_applies_even_when_persist_fails()
    {
        var runtime = new MemoryAiConnectionRuntime();
        var store = new FakeMemoryAiConnectionStore { SaveSucceeds = false, SaveError = "disk full" };
        var service = CreateService(runtime, new FakeAiModelCatalog { Models = ["gpt-oss:20b"] }, store);

        var response = await service.SetAsync(new MemoryAiConnectionRequest(
            "http://127.0.0.1:11434",
            "gpt-oss:20b",
            "nomic-embed-text"));

        Assert.Equal("http://127.0.0.1:11434", runtime.BaseUrl);
        Assert.False(response.Persisted);
        Assert.Equal("disk full", response.PersistError);
    }

    [Fact]
    public void FromStore_applies_saved_settings_on_startup()
    {
        var store = new FakeMemoryAiConnectionStore
        {
            Settings = new MemoryAiConnectionSettings(
                "http://192.168.1.2:11434",
                "gpt-oss:20b",
                "nomic-embed-text")
        };

        var runtime = MemoryAiConnectionRuntime.FromStore(store);

        Assert.Equal("http://192.168.1.2:11434", runtime.BaseUrl);
        Assert.Equal("gpt-oss:20b", runtime.ChatModel);
        Assert.Equal("nomic-embed-text", runtime.EmbeddingModel);
    }

    [Fact]
    public async Task Set_rejects_empty_chat_model()
    {
        var service = CreateService(new MemoryAiConnectionRuntime(), new FakeAiModelCatalog());

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetAsync(
            new MemoryAiConnectionRequest("http://127.0.0.1:11434", " ", "nomic-embed-text")));
    }

    private static MemoryAiConnectionService CreateService(
        MemoryAiConnectionRuntime runtime,
        FakeAiModelCatalog catalog,
        FakeMemoryAiConnectionStore? store = null)
    {
        var seed = new MemoryAiOptions
        {
            Provider = "Ollama",
            BaseUrl = "http://localhost:11434",
            ChatModel = "llama3.1",
            EmbeddingModel = "nomic-embed-text"
        };
        var monitor = new ApplyingOptionsMonitor(seed, runtime);
        return new MemoryAiConnectionService(
            monitor,
            new OptionsCache<MemoryAiOptions>(),
            runtime,
            new ChatModelResolver(catalog, monitor),
            store ?? new FakeMemoryAiConnectionStore());
    }

    private sealed class ApplyingOptionsMonitor(
        MemoryAiOptions seed,
        MemoryAiConnectionRuntime runtime) : IOptionsMonitor<MemoryAiOptions>
    {
        public MemoryAiOptions CurrentValue
        {
            get
            {
                var copy = new MemoryAiOptions
                {
                    Provider = seed.Provider,
                    BaseUrl = seed.BaseUrl,
                    ChatModel = seed.ChatModel,
                    EmbeddingModel = seed.EmbeddingModel
                };
                runtime.ApplyTo(copy);
                return copy;
            }
        }

        public MemoryAiOptions Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<MemoryAiOptions, string?> listener) =>
            FakeDisposable.Instance;

        private sealed class FakeDisposable : IDisposable
        {
            public static readonly FakeDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
