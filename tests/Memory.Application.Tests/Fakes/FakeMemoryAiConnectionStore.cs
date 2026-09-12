namespace Memory.Application.Tests.Fakes;

using Memory.Application.Runtime;

internal sealed class FakeMemoryAiConnectionStore : IMemoryAiConnectionStore
{
    public MemoryAiConnectionSettings? Settings { get; set; }
    public bool SaveSucceeds { get; set; } = true;
    public string? SaveError { get; set; }

    public MemoryAiConnectionSettings? Load() => Settings;

    public bool TrySave(MemoryAiConnectionSettings settings, out string? error)
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
