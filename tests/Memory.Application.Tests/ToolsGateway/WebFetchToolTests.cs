namespace Memory.Application.Tests.ToolsGateway;

using System.Net;
using System.Text;
using System.Text.Json;
using Memory.Application.Configuration;
using Memory.Application.ToolsGateway;
using Memory.Application.ToolsGateway.Web;
using Memory.Application.Tests.Fakes;

public sealed class WebFetchToolTests
{
    [Fact]
    public void Declares_web_read_and_not_network_local()
    {
        var tool = CreateTool();

        Assert.Equal(GatewayToolNames.WebFetch, tool.Name);
        Assert.Equal(ToolCapabilities.WebRead, Assert.Single(tool.RequiredCapabilities));
        Assert.DoesNotContain(ToolCapabilities.NetworkLocal, tool.RequiredCapabilities);
    }

    [Fact]
    public async Task Execute_rejects_loopback_without_fetching()
    {
        var http = new ScriptedRawHttpFetcher();
        var tool = CreateTool(http: http);

        await Assert.ThrowsAsync<UnsafeUrlException>(
            () => tool.ExecuteAsync(JsonDocument.Parse("""{"url":"http://127.0.0.1/"}""").RootElement));

        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task Execute_fetches_public_page()
    {
        var resolver = new FakeHostAddressResolver
        {
            Map = { ["example.com"] = [IPAddress.Parse("93.184.216.34")] }
        };
        var http = new ScriptedRawHttpFetcher();
        http.Enqueue(new RawHttpResponse(
            200,
            "text/html",
            null,
            Encoding.UTF8.GetBytes("<html><title>OK</title><body>Public</body></html>"),
            false));
        var tool = CreateTool(resolver, http);

        var payload = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"url":"https://example.com/page"}""").RootElement);

        var page = Assert.IsType<WebPageContent>(payload);
        Assert.Equal("OK", page.Title);
        Assert.Contains("Public", page.Text, StringComparison.Ordinal);
        Assert.Equal(200, page.StatusCode);
    }

    private static WebFetchTool CreateTool(
        FakeHostAddressResolver? resolver = null,
        ScriptedRawHttpFetcher? http = null)
    {
        resolver ??= new FakeHostAddressResolver();
        http ??= new ScriptedRawHttpFetcher();
        var fetcher = new WebContentFetcher(
            resolver,
            http,
            new FakeOptionsMonitor<ToolsOptions>(new ToolsOptions()));
        return new WebFetchTool(fetcher);
    }
}
