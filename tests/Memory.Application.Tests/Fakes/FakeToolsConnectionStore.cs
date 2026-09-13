namespace Memory.Application.Tests.Fakes;

using Memory.Application.Runtime;

internal sealed class FakeToolsConnectionStore : IToolsConnectionStore
{
    public ToolsConnectionSettings? Settings { get; set; }
    public bool SaveSucceeds { get; set; } = true;
    public string? SaveError { get; set; }

    public ToolsConnectionSettings? Load() => Settings;

    public bool TrySave(ToolsConnectionSettings settings, out string? error)
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

internal sealed class FakeSearXngReachabilityProbe : ISearXngReachabilityProbe
{
    public bool Reachable { get; set; }
    public string? Error { get; set; }
    public string? LastBaseUrl { get; private set; }

    public Task<(bool Reachable, string? Error)> ProbeAsync(
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastBaseUrl = baseUrl;
        return Task.FromResult((Reachable, Error));
    }
}
