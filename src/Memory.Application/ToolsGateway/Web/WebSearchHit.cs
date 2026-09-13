namespace Memory.Application.ToolsGateway.Web;

public sealed record WebSearchHit(
    string Title,
    string Url,
    string Snippet,
    string Source,
    double? Score);
