namespace Memory.Application.ToolsGateway.Web;

using System.Text.Json;
using Memory.Application.ToolsGateway;

internal sealed class WebFetchTool(IWebContentFetcher contentFetcher) : ITool
{
    public const string ToolName = GatewayToolNames.WebFetch;

    public string Name => ToolName;

    public string Description =>
        "Fetches one public http(s) page and returns cleaned text. Private, loopback, and local addresses are blocked.";

    public IReadOnlyCollection<string> RequiredCapabilities { get; } = [ToolCapabilities.WebRead];

    public IReadOnlyList<ToolParameterDescriptor> Parameters { get; } =
    [
        new("url", "string", "Absolute http or https URL of a public page.", Required: true)
    ];

    public async Task<object> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        var args = JsonArguments.Deserialize<WebFetchArguments>(arguments);
        return await FetchAsync(args, cancellationToken);
    }

    public Task<WebPageContent> FetchAsync(
        WebFetchArguments arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var uri = PublicNetworkPolicy.ParseFetchUri(arguments.Url);
        return contentFetcher.FetchAsync(uri, cancellationToken);
    }
}
