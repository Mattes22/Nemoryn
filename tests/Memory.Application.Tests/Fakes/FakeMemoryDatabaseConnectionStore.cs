namespace Memory.Application.Tests.Fakes;

using Memory.Application.Runtime;

internal sealed class FakeMemoryDatabaseConnectionStore : IMemoryDatabaseConnectionStore
{
    public MemoryDatabaseConnectionSettings? Settings { get; set; }
    public bool SaveSucceeds { get; set; } = true;
    public string? SaveError { get; set; }

    public MemoryDatabaseConnectionSettings? Load() => Settings;

    public bool TrySave(MemoryDatabaseConnectionSettings settings, out string? error)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!SaveSucceeds)
        {
            error = SaveError ?? "Unable to persist connection.";
            return false;
        }

        Settings = settings;
        error = null;
        return true;
    }
}
