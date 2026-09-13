namespace Memory.Infrastructure.ToolsGateway;

using System.Net;
using System.Net.Sockets;
using Memory.Application.ToolsGateway.Web;

internal static class PublicNetworkSockets
{
    public static SocketsHttpHandler Create(TimeSpan connectTimeout)
    {
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false,
            ConnectTimeout = connectTimeout,
            ConnectCallback = ConnectAsync
        };
    }

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var port = context.DnsEndPoint.Port;
        IPAddress[] addresses;
        if (IPAddress.TryParse(host.Trim('[', ']'), out var parsed))
        {
            addresses = [parsed];
        }
        else
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }

        var allowed = addresses.Where(address => !PublicNetworkPolicy.IsBlocked(address)).ToArray();
        if (allowed.Length == 0)
        {
            throw new HttpRequestException("Refusing to connect to a non-public address.");
        }

        var socket = new Socket(allowed[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };
        try
        {
            await socket.ConnectAsync(allowed[0], port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
