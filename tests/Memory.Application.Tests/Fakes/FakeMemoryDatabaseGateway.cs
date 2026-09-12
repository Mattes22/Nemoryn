namespace Memory.Application.Tests.Fakes;

using Memory.Application.Runtime;

internal sealed class FakeMemoryDatabaseGateway : IMemoryDatabaseGateway
{
    public MemoryDatabaseConnectionSettings Current { get; set; } = new(
        "localhost",
        5432,
        "memory",
        "memory_app",
        "secret");

    public bool Reachable { get; set; } = true;
    public string? ProbeError { get; set; }
    public Exception? MigrateException { get; set; }
    public int MigrateCalls { get; private set; }
    public MemoryDatabaseConnectionSettings? LastApplied { get; private set; }

    public void Apply(MemoryDatabaseConnectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        LastApplied = settings;
        Current = settings;
    }

    public Task<(bool Reachable, string? Error)> ProbeAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult((Reachable, Reachable ? null : ProbeError ?? "down"));
    }

    public Task ApplyPendingMigrationsAsync(CancellationToken cancellationToken = default)
    {
        MigrateCalls++;
        if (MigrateException is not null)
        {
            throw MigrateException;
        }

        return Task.CompletedTask;
    }
}
