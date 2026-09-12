namespace Memory.Infrastructure.Persistence;

using Memory.Application.Runtime;

internal sealed class MemoryDatabaseGateway(
    MemoryDatabaseConnectionRuntime runtime,
    IServiceProvider services) : IMemoryDatabaseGateway
{
    public MemoryDatabaseConnectionSettings Current => runtime.Current;

    public void Apply(MemoryDatabaseConnectionSettings settings) => runtime.Apply(settings);

    public async Task<(bool Reachable, string? Error)> ProbeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            await using var connection = runtime.DataSource.CreateConnection();
            await connection.OpenAsync(timeout.Token);
            return (true, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (false, "Databáze neodpověděla včas.");
        }
        catch (Exception exception)
        {
            return (false, exception.GetBaseException().Message);
        }
    }

    public Task ApplyPendingMigrationsAsync(CancellationToken cancellationToken = default)
    {
        return MemorySchema.ApplyPendingMigrationsAsync(services, cancellationToken);
    }
}
