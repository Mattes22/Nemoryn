namespace Memory.Application.Tests.Tools;

using System.Net.Http;
using System.Text.Json;
using Memory.Application.Tools;
using Memory.Application.ToolsGateway.Web;
using Memory.Application.Tests.Fakes;

public sealed class WebSearchAgentToolTests
{
    [Fact]
    public void Declares_builtin_web_search_capability()
    {
        var tool = new WebSearchAgentTool(new FakeWebSearchProvider());

        Assert.Equal(WebSearchAgentTool.ToolName, tool.Definition.Name);
        Assert.Equal(ToolTrust.Builtin, tool.Definition.Trust);
        Assert.Equal(ToolCapability.WebSearch, Assert.Single(tool.Definition.Capabilities));
        Assert.DoesNotContain('.', tool.Definition.Name);
    }

    [Fact]
    public async Task Invoke_passes_trimmed_query_and_serializes_hits()
    {
        var provider = new FakeWebSearchProvider
        {
            Result = new WebSearchResult(
                "Teuta Ganna",
                "SearXNG",
                [new WebSearchHit("Title", "https://example.com", "Snippet", "duckduckgo", 1)])
        };
        var tool = new WebSearchAgentTool(provider);

        var result = await tool.InvokeAsync(
            new ToolCall("call_1", WebSearchAgentTool.ToolName, """{"query":"  Teuta Ganna  ","maxResults":99}"""),
            ToolContext.None);

        Assert.True(result.Ok);
        Assert.Equal("Teuta Ganna", provider.LastQuery);
        Assert.Equal(WebSearchTool.MaxResultsCap, provider.LastMaxResults);
        using var document = JsonDocument.Parse(result.Content);
        Assert.Equal("https://example.com", document.RootElement.GetProperty("results")[0].GetProperty("url").GetString());
    }

    [Fact]
    public async Task Invoke_returns_failed_result_when_searxng_is_not_configured()
    {
        var provider = new FakeWebSearchProvider
        {
            Exception = new InvalidOperationException("Tools:Web:SearXNG:BaseUrl is not configured.")
        };
        var tool = new WebSearchAgentTool(provider);

        var result = await tool.InvokeAsync(
            new ToolCall("call_1", WebSearchAgentTool.ToolName, """{"query":"Teuta Ganna"}"""),
            ToolContext.None);

        Assert.False(result.Ok);
        Assert.Contains("SearXNG:BaseUrl", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invoke_returns_failed_result_when_search_backend_errors()
    {
        var provider = new FakeWebSearchProvider
        {
            Exception = new HttpRequestException("SearXNG returned HTTP 502.")
        };
        var tool = new WebSearchAgentTool(provider);

        var result = await tool.InvokeAsync(
            new ToolCall("call_1", WebSearchAgentTool.ToolName, """{"query":"Teuta Ganna"}"""),
            ToolContext.None);

        Assert.False(result.Ok);
        Assert.Contains("502", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invoke_requires_query()
    {
        var tool = new WebSearchAgentTool(new FakeWebSearchProvider());

        var result = await tool.InvokeAsync(
            new ToolCall("call_1", WebSearchAgentTool.ToolName, """{"query":"   "}"""),
            ToolContext.None);

        Assert.False(result.Ok);
        Assert.Equal("query is required.", result.Content);
    }
}
