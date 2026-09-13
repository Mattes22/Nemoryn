namespace Memory.Application.ToolsGateway.Web;

public interface IWebSearchProvider
{
    string Name { get; }

    Task<WebSearchResult> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default);
}
