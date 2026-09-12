namespace Memory.Application.Tests.Runtime;

using Memory.Application.Runtime;
using Memory.Application.Tests.Fakes;

public sealed class MemoryDatabaseConnectionServiceTests
{
    [Fact]
    public async Task Get_hides_password()
    {
        var gateway = new FakeMemoryDatabaseGateway();
        var service = new MemoryDatabaseConnectionService(gateway, new FakeMemoryDatabaseConnectionStore());

        var response = await service.GetAsync();

        Assert.Equal("localhost", response.Host);
        Assert.True(response.PasswordSet);
        Assert.True(response.Reachable);
        Assert.False(response.Persisted);
        Assert.DoesNotContain("secret", response.Host, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Set_applies_persists_and_migrates_when_reachable()
    {
        var gateway = new FakeMemoryDatabaseGateway();
        var store = new FakeMemoryDatabaseConnectionStore();
        var service = new MemoryDatabaseConnectionService(gateway, store);

        var response = await service.SetAsync(new MemoryDatabaseConnectionRequest(
            "postgres",
            5432,
            "nemoryn",
            "nemoryn",
            "docker"));

        Assert.Equal("postgres", gateway.Current.Host);
        Assert.Equal("docker", gateway.Current.Password);
        Assert.Equal("postgres", store.Settings?.Host);
        Assert.True(response.Persisted);
        Assert.True(response.Reachable);
        Assert.True(response.PasswordSet);
        Assert.Equal(1, gateway.MigrateCalls);
    }

    [Fact]
    public async Task Set_skips_migrate_when_unreachable()
    {
        var gateway = new FakeMemoryDatabaseGateway { Reachable = false, ProbeError = "connection refused" };
        var service = new MemoryDatabaseConnectionService(gateway, new FakeMemoryDatabaseConnectionStore());

        var response = await service.SetAsync(new MemoryDatabaseConnectionRequest(
            "db.lan",
            5432,
            "memory",
            "app",
            "x"));

        Assert.Equal("db.lan", gateway.LastApplied?.Host);
        Assert.False(response.Reachable);
        Assert.Equal("connection refused", response.ReachError);
        Assert.Equal(0, gateway.MigrateCalls);
    }

    [Fact]
    public async Task Set_applies_even_when_persist_fails()
    {
        var gateway = new FakeMemoryDatabaseGateway();
        var store = new FakeMemoryDatabaseConnectionStore { SaveSucceeds = false, SaveError = "disk full" };
        var service = new MemoryDatabaseConnectionService(gateway, store);

        var response = await service.SetAsync(new MemoryDatabaseConnectionRequest(
            "postgres",
            5432,
            "nemoryn",
            "nemoryn",
            "docker"));

        Assert.Equal("postgres", gateway.Current.Host);
        Assert.False(response.Persisted);
        Assert.Equal("disk full", response.PersistError);
        Assert.Equal(1, gateway.MigrateCalls);
    }
}
