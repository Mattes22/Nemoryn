namespace Memory.Application.Tests.ToolsGateway;

using System.Net;
using System.Text;
using Memory.Application.Configuration;
using Memory.Application.ToolsGateway;
using Memory.Application.ToolsGateway.Web;
using Memory.Application.Tests.Fakes;

public sealed class WebContentFetcherSsrfTests
{
    [Fact]
    public async Task Blocks_loopback_ipv4_before_http()
    {
        var http = new ScriptedRawHttpFetcher();
        var fetcher = CreateFetcher(new FakeHostAddressResolver(), http);

        var exception = await Assert.ThrowsAsync<UnsafeUrlException>(
            () => fetcher.FetchAsync(new Uri("http://127.0.0.1/secret")));

        Assert.Equal("URL is not allowed.", exception.Message);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task Blocks_rfc1918_ipv4_before_http()
    {
        var http = new ScriptedRawHttpFetcher();
        var fetcher = CreateFetcher(new FakeHostAddressResolver(), http);

        await Assert.ThrowsAsync<UnsafeUrlException>(
            () => fetcher.FetchAsync(new Uri("http://192.168.1.20/admin")));
        await Assert.ThrowsAsync<UnsafeUrlException>(
            () => fetcher.FetchAsync(new Uri("http://10.0.0.5/admin")));
        await Assert.ThrowsAsync<UnsafeUrlException>(
            () => fetcher.FetchAsync(new Uri("http://172.16.4.2/admin")));

        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task Blocks_ipv6_loopback_before_http()
    {
        var http = new ScriptedRawHttpFetcher();
        var fetcher = CreateFetcher(new FakeHostAddressResolver(), http);

        await Assert.ThrowsAsync<UnsafeUrlException>(
            () => fetcher.FetchAsync(new Uri("http://[::1]/secret")));

        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task Blocks_hostname_that_resolves_to_private_ip()
    {
        var resolver = new FakeHostAddressResolver
        {
            Map = { ["evil.example"] = [IPAddress.Parse("10.1.2.3")] }
        };
        var http = new ScriptedRawHttpFetcher();
        var fetcher = CreateFetcher(resolver, http);

        var exception = await Assert.ThrowsAsync<UnsafeUrlException>(
            () => fetcher.FetchAsync(new Uri("https://evil.example/")));

        Assert.Equal("URL is not allowed.", exception.Message);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task Allows_public_https_url()
    {
        var resolver = new FakeHostAddressResolver
        {
            Map = { ["example.com"] = [IPAddress.Parse("93.184.216.34")] }
        };
        var http = new ScriptedRawHttpFetcher();
        http.Enqueue(new RawHttpResponse(
            200,
            "text/html; charset=utf-8",
            Location: null,
            Encoding.UTF8.GetBytes("<html><title>Example</title><body><script>alert(1)</script><p>Hello world</p></body></html>"),
            Truncated: false));
        var fetcher = CreateFetcher(resolver, http);

        var page = await fetcher.FetchAsync(new Uri("https://example.com/hello"));

        Assert.Equal("https://example.com/hello", page.Url);
        Assert.Equal("Example", page.Title);
        Assert.Equal(200, page.StatusCode);
        Assert.Equal("text/html; charset=utf-8", page.ContentType);
        Assert.Contains("Hello world", page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("alert", page.Text, StringComparison.Ordinal);
        Assert.Equal(new Uri("https://example.com/hello"), Assert.Single(http.Requests));
    }

    [Fact]
    public async Task Blocks_redirect_from_public_url_to_loopback()
    {
        var resolver = new FakeHostAddressResolver
        {
            Map = { ["example.com"] = [IPAddress.Parse("93.184.216.34")] }
        };
        var http = new ScriptedRawHttpFetcher();
        http.Enqueue(new RawHttpResponse(302, "text/html", "http://127.0.0.1/secret", [], false));
        var fetcher = CreateFetcher(resolver, http);

        var exception = await Assert.ThrowsAsync<UnsafeUrlException>(
            () => fetcher.FetchAsync(new Uri("https://example.com/go")));

        Assert.Equal("URL is not allowed.", exception.Message);
        Assert.Equal(new Uri("https://example.com/go"), Assert.Single(http.Requests));
    }

    [Fact]
    public async Task Blocks_redirect_from_public_url_to_rfc1918()
    {
        var resolver = new FakeHostAddressResolver
        {
            Map = { ["example.com"] = [IPAddress.Parse("93.184.216.34")] }
        };
        var http = new ScriptedRawHttpFetcher();
        http.Enqueue(new RawHttpResponse(301, "text/html", "http://192.168.0.10/internal", [], false));
        var fetcher = CreateFetcher(resolver, http);

        await Assert.ThrowsAsync<UnsafeUrlException>(
            () => fetcher.FetchAsync(new Uri("https://example.com/go")));

        Assert.Single(http.Requests);
    }

    [Fact]
    public async Task Blocks_redirect_to_hostname_that_resolves_private()
    {
        var resolver = new FakeHostAddressResolver
        {
            Map =
            {
                ["example.com"] = [IPAddress.Parse("93.184.216.34")],
                ["intranet.example"] = [IPAddress.Parse("10.0.0.8")]
            }
        };
        var http = new ScriptedRawHttpFetcher();
        http.Enqueue(new RawHttpResponse(307, "text/html", "https://intranet.example/admin", [], false));
        var fetcher = CreateFetcher(resolver, http);

        await Assert.ThrowsAsync<UnsafeUrlException>(
            () => fetcher.FetchAsync(new Uri("https://example.com/go")));

        Assert.Single(http.Requests);
    }

    private static WebContentFetcher CreateFetcher(
        IHostAddressResolver resolver,
        IRawHttpFetcher http)
    {
        return new WebContentFetcher(
            resolver,
            http,
            new FakeOptionsMonitor<ToolsOptions>(new ToolsOptions()));
    }
}
