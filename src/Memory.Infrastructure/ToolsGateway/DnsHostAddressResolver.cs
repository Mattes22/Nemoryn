namespace Memory.Infrastructure.ToolsGateway;

using System.Net;
using Memory.Application.ToolsGateway.Web;

internal sealed class DnsHostAddressResolver : IHostAddressResolver
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return [];
        }

        var value = host.Trim().Trim('[', ']');
        if (IPAddress.TryParse(value, out var address))
        {
            return [address];
        }

        var addresses = await Dns.GetHostAddressesAsync(value, cancellationToken);
        return addresses;
    }
}
