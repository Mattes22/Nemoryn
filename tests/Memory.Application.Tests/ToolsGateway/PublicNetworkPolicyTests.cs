namespace Memory.Application.Tests.ToolsGateway;

using System.Net;
using Memory.Application.ToolsGateway.Web;

public sealed class PublicNetworkPolicyTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.2")]
    [InlineData("0.0.0.0")]
    [InlineData("10.0.0.1")]
    [InlineData("10.255.255.254")]
    [InlineData("192.168.0.1")]
    [InlineData("192.168.1.20")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    public void Blocks_private_and_local_ipv4(string value)
    {
        Assert.True(PublicNetworkPolicy.IsBlocked(IPAddress.Parse(value)));
    }

    [Theory]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456:789a::1")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:192.168.1.1")]
    [InlineData("::ffff:10.1.2.3")]
    public void Blocks_private_and_local_ipv6(string value)
    {
        Assert.True(PublicNetworkPolicy.IsBlocked(IPAddress.Parse(value)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")]
    [InlineData("2001:4860:4860::8888")]
    public void Allows_public_addresses(string value)
    {
        Assert.False(PublicNetworkPolicy.IsBlocked(IPAddress.Parse(value)));
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("LOCALHOST")]
    [InlineData("app.localhost")]
    [InlineData("printer.local")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void Blocks_localhost_hostnames(string host)
    {
        Assert.True(PublicNetworkPolicy.IsBlockedHostName(host));
    }

    [Fact]
    public void ParseFetchUri_rejects_non_http_schemes_and_credentials()
    {
        Assert.Throws<Memory.Application.ToolsGateway.UnsafeUrlException>(
            () => PublicNetworkPolicy.ParseFetchUri("file:///etc/passwd"));
        Assert.Throws<Memory.Application.ToolsGateway.UnsafeUrlException>(
            () => PublicNetworkPolicy.ParseFetchUri("ftp://example.com/file"));
        Assert.Throws<Memory.Application.ToolsGateway.UnsafeUrlException>(
            () => PublicNetworkPolicy.ParseFetchUri("https://user:pass@example.com/"));
        Assert.Throws<Memory.Application.ToolsGateway.UnsafeUrlException>(
            () => PublicNetworkPolicy.ParseFetchUri("http://127.0.0.1/secret"));
    }

    [Fact]
    public void ParseFetchUri_allows_public_https()
    {
        var uri = PublicNetworkPolicy.ParseFetchUri("https://example.com/path?q=1");

        Assert.Equal("https://example.com/path?q=1", uri.ToString());
    }
}
