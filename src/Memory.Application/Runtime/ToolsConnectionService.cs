namespace Memory.Application.Runtime;

using Microsoft.Extensions.Options;
using Memory.Application.Configuration;

internal sealed class ToolsConnectionService(
    IOptionsMonitor<ToolsOptions> toolsOptions,
    IOptionsMonitorCache<ToolsOptions> cache,
    ToolsConnectionRuntime runtime,
    IToolsConnectionStore store,
    ISearXngReachabilityProbe reachabilityProbe) : IToolsConnectionService
{
    public async Task<ToolsConnectionResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var options = toolsOptions.CurrentValue;
        var baseUrl = options.Web.SearXNG.BaseUrl?.Trim() ?? string.Empty;
        var (reachable, reachError) = await reachabilityProbe.ProbeAsync(baseUrl, cancellationToken);
        return ToResponse(options, reachable, reachError, store.Load(), persistError: null);
    }

    public async Task<ToolsConnectionResponse> SetAsync(
        ToolsConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = new ToolsConnectionSettings(ToolsSearXngBaseUrl.Normalize(request.SearXngBaseUrl));
        runtime.HasSearXngBaseUrlOverride = true;
        runtime.SearXngBaseUrl = settings.SearXngBaseUrl;
        var persisted = store.TrySave(settings, out var persistError);
        cache.TryRemove(Options.DefaultName);

        var options = toolsOptions.CurrentValue;
        var (reachable, reachError) = await reachabilityProbe.ProbeAsync(settings.SearXngBaseUrl, cancellationToken);
        return ToResponse(options, reachable, reachError, persisted ? settings : store.Load(), persistError);
    }

    private static ToolsConnectionResponse ToResponse(
        ToolsOptions options,
        bool reachable,
        string? reachError,
        ToolsConnectionSettings? stored,
        string? persistError)
    {
        var baseUrl = options.Web.SearXNG.BaseUrl?.Trim() ?? string.Empty;
        var persisted = stored is not null
            && string.Equals(stored.SearXngBaseUrl, baseUrl, StringComparison.Ordinal);
        var configured = ToolsSearXngBaseUrl.TryCreate(baseUrl, out _);
        var provider = string.IsNullOrWhiteSpace(options.Web.SearchProvider)
            ? "SearXNG"
            : options.Web.SearchProvider.Trim();

        return new ToolsConnectionResponse(
            provider,
            baseUrl,
            configured,
            reachable,
            configured ? reachError : null,
            persisted,
            persistError);
    }
}
