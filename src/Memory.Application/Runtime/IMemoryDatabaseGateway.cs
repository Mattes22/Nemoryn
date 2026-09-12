namespace Memory.Application.Runtime;

public interface IMemoryDatabaseGateway
{
    MemoryDatabaseConnectionSettings Current { get; }

    void Apply(MemoryDatabaseConnectionSettings settings);

    Task<(bool Reachable, string? Error)> ProbeAsync(CancellationToken cancellationToken = default);

    Task ApplyPendingMigrationsAsync(CancellationToken cancellationToken = default);
}
