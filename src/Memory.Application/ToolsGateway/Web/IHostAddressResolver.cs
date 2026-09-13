namespace Memory.Application.ToolsGateway.Web;

using System.Net;

public interface IHostAddressResolver
{
    Task<IReadOnlyList<IPAddress>> ResolveAsync(
        string host,
        CancellationToken cancellationToken = default);
}
