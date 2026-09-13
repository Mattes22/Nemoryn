namespace Memory.Application.ToolsGateway.Web;

using System.Net;
using System.Net.Sockets;
using Memory.Application.ToolsGateway;

public static class PublicNetworkPolicy
{
    public static bool IsBlocked(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.None))
        {
            return true;
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsBlockedIPv4(address),
            AddressFamily.InterNetworkV6 => IsBlockedIPv6(address),
            _ => true
        };
    }

    public static bool IsBlockedHostName(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        var value = host.Trim().Trim('[', ']');
        if (value.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || value.Equals("ip6-localhost", StringComparison.OrdinalIgnoreCase)
            || value.Equals("ip6-loopback", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IPAddress.TryParse(value, out var address))
        {
            return IsBlocked(address);
        }

        return false;
    }

    public static void EnsureFetchUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new UnsafeUrlException("Only absolute http and https URLs are allowed.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new UnsafeUrlException("URLs with credentials are not allowed.");
        }

        if (IsBlockedHostName(uri.IdnHost))
        {
            throw new UnsafeUrlException("URL is not allowed.");
        }
    }

    public static Uri ParseFetchUri(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri))
        {
            throw new UnsafeUrlException("URL must be an absolute http(s) URI.");
        }

        EnsureFetchUri(uri);
        return uri;
    }

    private static bool IsBlockedIPv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes[0] == 0 || bytes[0] == 10 || bytes[0] == 127)
        {
            return true;
        }

        if (bytes[0] == 169 && bytes[1] == 254)
        {
            return true;
        }

        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
        {
            return true;
        }

        if (bytes[0] == 192 && bytes[1] == 168)
        {
            return true;
        }

        if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
        {
            return true;
        }

        return bytes[0] >= 224;
    }

    private static bool IsBlockedIPv6(IPAddress address)
    {
        return address.IsIPv6LinkLocal
            || address.IsIPv6SiteLocal
            || address.IsIPv6Multicast
            || address.IsIPv6UniqueLocal;
    }
}
