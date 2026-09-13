namespace Memory.Application.Tests.Tools;

using Memory.Application.Tools;
using Memory.Application.ToolsGateway.Web;
using Memory.Application.Tests.Fakes;

public sealed class WebFetchAgentToolTests
{
    [Fact]
    public void Declares_builtin_web_read_capability()
    {
        var tool = new WebFetchAgentTool(new FakeWebContentFetcher());

        Assert.Equal(WebFetchAgentTool.ToolName, tool.Definition.Name);
        Assert.Equal(ToolTrust.Builtin, tool.Definition.Trust);
        Assert.Equal(ToolCapability.WebRead, Assert.Single(tool.Definition.Capabilities));
        Assert.DoesNotContain('.', tool.Definition.Name);
    }

    [Fact]
    public async Task Invoke_fetches_a_public_url()
    {
        var fetcher = new FakeWebContentFetcher
        {
            Result = new WebPageContent("https://example.com/page", "Example", "Hello", 200, "text/html")
        };
        var tool = new WebFetchAgentTool(fetcher);

        var result = await tool.InvokeAsync(
            new ToolCall("call_1", WebFetchAgentTool.ToolName, """{"url":"https://example.com/page"}"""),
            ToolContext.None);

        Assert.True(result.Ok);
        Assert.Equal(new Uri("https://example.com/page"), fetcher.LastUri);
        Assert.Contains("Hello", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invoke_rejects_loopback_urls()
    {
        var fetcher = new FakeWebContentFetcher();
        var tool = new WebFetchAgentTool(fetcher);

        var result = await tool.InvokeAsync(
            new ToolCall("call_1", WebFetchAgentTool.ToolName, """{"url":"http://127.0.0.1/secret"}"""),
            ToolContext.None);

        Assert.False(result.Ok);
        Assert.Null(fetcher.LastUri);
        Assert.Contains("not allowed", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invoke_requires_url()
    {
        var tool = new WebFetchAgentTool(new FakeWebContentFetcher());

        var result = await tool.InvokeAsync(
            new ToolCall("call_1", WebFetchAgentTool.ToolName, "{}"),
            ToolContext.None);

        Assert.False(result.Ok);
        Assert.Equal("url is required.", result.Content);
    }
}
