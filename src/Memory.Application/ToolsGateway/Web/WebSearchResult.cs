namespace Memory.Application.ToolsGateway.Web;

public sealed record WebSearchResult(
    string Query,
    string Provider,
    IReadOnlyList<WebSearchHit> Results);
