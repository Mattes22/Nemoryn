namespace Memory.Application.Runtime;

public sealed record ToolsConnectionResponse(
    string SearchProvider,
    string SearXngBaseUrl,
    bool Configured,
    bool Reachable,
    string? ReachError,
    bool Persisted,
    string? PersistError);

public sealed record ToolsConnectionRequest(string? SearXngBaseUrl);

public interface IToolsConnectionService
{
    Task<ToolsConnectionResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<ToolsConnectionResponse> SetAsync(
        ToolsConnectionRequest request,
        CancellationToken cancellationToken = default);
}

public interface ISearXngReachabilityProbe
{
    Task<(bool Reachable, string? Error)> ProbeAsync(
        string baseUrl,
        CancellationToken cancellationToken = default);
}
