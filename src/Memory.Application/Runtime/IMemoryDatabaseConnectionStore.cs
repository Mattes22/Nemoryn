namespace Memory.Application.Runtime;

public sealed record MemoryDatabaseConnectionSettings(
    string Host,
    int Port,
    string Database,
    string Username,
    string Password);

public interface IMemoryDatabaseConnectionStore
{
    MemoryDatabaseConnectionSettings? Load();
    bool TrySave(MemoryDatabaseConnectionSettings settings, out string? error);
}
