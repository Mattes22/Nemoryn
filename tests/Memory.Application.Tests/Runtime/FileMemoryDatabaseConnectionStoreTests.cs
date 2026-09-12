namespace Memory.Application.Tests.Runtime;

using Memory.Application.Runtime;

public sealed class FileMemoryDatabaseConnectionStoreTests
{
    [Fact]
    public void Save_then_load_roundtrips_settings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"memory-db-{Guid.NewGuid():N}.json");
        try
        {
            var store = new FileMemoryDatabaseConnectionStore(path);
            Assert.True(store.TrySave(
                new MemoryDatabaseConnectionSettings("postgres", 5432, "nemoryn", "nemoryn", "s3cret"),
                out var error));
            Assert.Null(error);

            var loaded = store.Load();
            Assert.NotNull(loaded);
            Assert.Equal("postgres", loaded.Host);
            Assert.Equal(5432, loaded.Port);
            Assert.Equal("nemoryn", loaded.Database);
            Assert.Equal("nemoryn", loaded.Username);
            Assert.Equal("s3cret", loaded.Password);
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
        var store = new FileMemoryDatabaseConnectionStore(
            Path.Combine(Path.GetTempPath(), $"memory-db-missing-{Guid.NewGuid():N}.json"));

        Assert.Null(store.Load());
    }
}
