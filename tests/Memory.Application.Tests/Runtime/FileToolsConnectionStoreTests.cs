namespace Memory.Application.Tests.Runtime;

using Memory.Application.Runtime;

public sealed class FileToolsConnectionStoreTests
{
    [Fact]
    public void Save_then_load_roundtrips_settings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"memory-tools-{Guid.NewGuid():N}.json");
        try
        {
            var store = new FileToolsConnectionStore(path);
            Assert.True(store.TrySave(new ToolsConnectionSettings("http://192.168.1.2:8080"), out var error));
            Assert.Null(error);

            var loaded = store.Load();
            Assert.NotNull(loaded);
            Assert.Equal("http://192.168.1.2:8080", loaded.SearXngBaseUrl);
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
        var store = new FileToolsConnectionStore(
            Path.Combine(Path.GetTempPath(), $"memory-tools-missing-{Guid.NewGuid():N}.json"));

        Assert.Null(store.Load());
    }
}
