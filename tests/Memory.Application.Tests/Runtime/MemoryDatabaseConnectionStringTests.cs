namespace Memory.Application.Tests.Runtime;

using Memory.Application.Runtime;

public sealed class MemoryDatabaseConnectionStringTests
{
    [Fact]
    public void Parse_reads_host_port_database_and_user_id()
    {
        var settings = MemoryDatabaseConnectionString.Parse(
            "Host=postgres;Port=5433;Database=nemoryn;User ID=nemoryn;Password=s3cret");

        Assert.Equal("postgres", settings.Host);
        Assert.Equal(5433, settings.Port);
        Assert.Equal("nemoryn", settings.Database);
        Assert.Equal("nemoryn", settings.Username);
        Assert.Equal("s3cret", settings.Password);
    }

    [Fact]
    public void Build_roundtrips_parse()
    {
        var original = new MemoryDatabaseConnectionSettings("db.lan", 5432, "memory", "app", "p@ss");
        var parsed = MemoryDatabaseConnectionString.Parse(MemoryDatabaseConnectionString.Build(original));

        Assert.Equal(original, parsed);
    }

    [Fact]
    public void FromRequest_keeps_password_when_blank()
    {
        var current = new MemoryDatabaseConnectionSettings("localhost", 5432, "memory", "app", "keep-me");
        var merged = MemoryDatabaseConnectionString.FromRequest(
            new MemoryDatabaseConnectionRequest("postgres", 5432, "nemoryn", "nemoryn", "  "),
            current);

        Assert.Equal("postgres", merged.Host);
        Assert.Equal("nemoryn", merged.Database);
        Assert.Equal("keep-me", merged.Password);
    }

    [Fact]
    public void Parse_rejects_missing_host()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => MemoryDatabaseConnectionString.Parse("Database=memory;Username=app"));

        Assert.Contains("host", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
