namespace Memory.Application.Tests.Runtime;

using Memory.Application.Runtime;

public sealed class FileMemoryAiConnectionStoreTests
{
    [Fact]
    public void Save_then_load_roundtrips_settings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"memory-ai-{Guid.NewGuid():N}.json");
        try
        {
            var store = new FileMemoryAiConnectionStore(path);
            Assert.True(store.TrySave(
                new MemoryAiConnectionSettings("http://192.168.1.2:11434", "gpt-oss:20b", "nomic-embed-text"),
                out var error));
            Assert.Null(error);

            var loaded = store.Load();
            Assert.NotNull(loaded);
            Assert.Equal("http://192.168.1.2:11434", loaded.BaseUrl);
            Assert.Equal("gpt-oss:20b", loaded.ChatModel);
            Assert.Equal("nomic-embed-text", loaded.EmbeddingModel);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Load_returns_null_when_file_is_missing()
    {
        var store = new FileMemoryAiConnectionStore(
            Path.Combine(Path.GetTempPath(), $"memory-ai-missing-{Guid.NewGuid():N}.json"));

        Assert.Null(store.Load());
    }
}
