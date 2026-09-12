namespace Memory.Application.Runtime;

internal sealed class MemoryDatabaseConnectionService(
    IMemoryDatabaseGateway gateway,
    IMemoryDatabaseConnectionStore store) : IMemoryDatabaseConnectionService
{
    public async Task<MemoryDatabaseConnectionResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var (reachable, reachError) = await gateway.ProbeAsync(cancellationToken);
        return ToResponse(gateway.Current, reachable, reachError, store.Load(), persistError: null);
    }

    public async Task<MemoryDatabaseConnectionResponse> SetAsync(
        MemoryDatabaseConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = MemoryDatabaseConnectionString.FromRequest(request, gateway.Current);
        gateway.Apply(settings);
        var persisted = store.TrySave(settings, out var persistError);
        var (reachable, reachError) = await gateway.ProbeAsync(cancellationToken);
        if (reachable)
        {
            try
            {
                await gateway.ApplyPendingMigrationsAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                reachError = exception.GetBaseException().Message;
            }
        }

        return ToResponse(settings, reachable, reachError, persisted ? settings : store.Load(), persistError);
    }

    private static MemoryDatabaseConnectionResponse ToResponse(
        MemoryDatabaseConnectionSettings current,
        bool reachable,
        string? reachError,
        MemoryDatabaseConnectionSettings? stored,
        string? persistError)
    {
        var persisted = stored is not null
            && string.Equals(stored.Host, current.Host, StringComparison.OrdinalIgnoreCase)
            && stored.Port == current.Port
            && string.Equals(stored.Database, current.Database, StringComparison.Ordinal)
            && string.Equals(stored.Username, current.Username, StringComparison.Ordinal)
            && stored.Password == current.Password;

        return new MemoryDatabaseConnectionResponse(
            current.Host,
            current.Port,
            current.Database,
            current.Username,
            PasswordSet: current.Password.Length > 0,
            reachable,
            reachError,
            persisted,
            persistError);
    }
}
