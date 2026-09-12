namespace Memory.Application.Runtime;

public sealed record MemoryDatabaseConnectionResponse(
    string Host,
    int Port,
    string Database,
    string Username,
    bool PasswordSet,
    bool Reachable,
    string? ReachError,
    bool Persisted,
    string? PersistError);

public sealed record MemoryDatabaseConnectionRequest(
    string Host,
    int Port,
    string Database,
    string Username,
    string? Password);

public interface IMemoryDatabaseConnectionService
{
    Task<MemoryDatabaseConnectionResponse> GetAsync(CancellationToken cancellationToken = default);
    Task<MemoryDatabaseConnectionResponse> SetAsync(
        MemoryDatabaseConnectionRequest request,
        CancellationToken cancellationToken = default);
}
