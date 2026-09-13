namespace Memory.Application.ToolsGateway.Web;

public interface IWebContentFetcher
{
    Task<WebPageContent> FetchAsync(
        Uri uri,
        CancellationToken cancellationToken = default);
}
