namespace Memory.Application.ToolsGateway.Web;

using System.Text.Json;
using Memory.Application.ToolsGateway;

internal sealed class WebSearchTool(IWebSearchProvider searchProvider) : ITool
{
    public const string ToolName = GatewayToolNames.WebSearch;
    public const int DefaultMaxResults = 5;
    public const int MaxResultsCap = 10;
    public const int MaxQueryChars = 500;

    public string Name => ToolName;

    public string Description =>
        "Full-text web search. Returns structured hits (title, url, snippet, source, score). Not a general HTTP client.";

    public IReadOnlyCollection<string> RequiredCapabilities { get; } = [ToolCapabilities.WebSearch];

    public IReadOnlyList<ToolParameterDescriptor> Parameters { get; } =
    [
        new("query", "string", "Search query.", Required: true),
        new("maxResults", "integer", "Maximum hits to return. Defaults to 5, capped at 10.")
    ];

    public async Task<object> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        var args = JsonArguments.Deserialize<WebSearchArguments>(arguments);
        return await SearchAsync(args, cancellationToken);
    }

    public Task<WebSearchResult> SearchAsync(
        WebSearchArguments arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var query = arguments.Query?.Trim() ?? string.Empty;
        if (query.Length == 0)
        {
            throw new ArgumentException("query is required.");
        }

        if (query.Length > MaxQueryChars)
        {
            throw new ArgumentException($"query must be at most {MaxQueryChars} characters.");
        }

        return searchProvider.SearchAsync(query, NormalizeMaxResults(arguments.MaxResults), cancellationToken);
    }

    private static int NormalizeMaxResults(int? maxResults)
    {
        if (maxResults is null || maxResults <= 0)
        {
            return DefaultMaxResults;
        }

        return Math.Min(maxResults.Value, MaxResultsCap);
    }
}
