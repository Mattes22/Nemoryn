namespace Memory.Application.ToolsGateway.Web;

public interface IRawHttpFetcher
{
    Task<RawHttpResponse> SendAsync(
        Uri uri,
        int maxResponseBytes,
        CancellationToken cancellationToken = default);
}
