namespace Memory.Application.Tests.ToolsGateway;

using System.Text.Json;
using Memory.Application.ToolsGateway;
using Memory.Application.ToolsGateway.Web;
using Memory.Application.Tests.Fakes;

public sealed class WebSearchToolTests
{
    [Fact]
    public void Declares_web_search_capability_only()
    {
        var tool = new WebSearchTool(new FakeWebSearchProvider());

        Assert.Equal(GatewayToolNames.WebSearch, tool.Name);
        Assert.Equal(ToolCapabilities.WebSearch, Assert.Single(tool.RequiredCapabilities));
    }

    [Fact]
    public async Task Execute_passes_trimmed_query_and_capped_max_results()
    {
        var provider = new FakeWebSearchProvider
        {
            Result = new WebSearchResult(
                "nemoryn",
                "SearXNG",
                [new WebSearchHit("Title", "https://example.com", "Snippet", "duckduckgo", 1)])
        };
        var tool = new WebSearchTool(provider);

        var payload = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"query":"  nemoryn  ","maxResults":99}""").RootElement);

        var result = Assert.IsType<WebSearchResult>(payload);
        Assert.Equal("nemoryn", provider.LastQuery);
        Assert.Equal(WebSearchTool.MaxResultsCap, provider.LastMaxResults);
        Assert.Equal("SearXNG", result.Provider);
        Assert.Equal("https://example.com", result.Results.Single().Url);
    }

    [Fact]
    public async Task Execute_requires_query()
    {
        var tool = new WebSearchTool(new FakeWebSearchProvider());

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => tool.ExecuteAsync(JsonDocument.Parse("""{"query":"   "}""").RootElement));

        Assert.Contains("query is required", exception.Message, StringComparison.Ordinal);
    }
}
