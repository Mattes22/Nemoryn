namespace Memory.Application.Tests.ToolsGateway;

using Memory.Application.ToolsGateway;
using Memory.Application.Tests.Fakes;

public sealed class ToolRegistryTests
{
    [Fact]
    public void ListAvailable_returns_only_tools_covered_by_granted_capabilities()
    {
        var search = new FakeGatewayTool("web.search", ToolCapabilities.WebSearch);
        var fetch = new FakeGatewayTool("web.fetch", ToolCapabilities.WebRead);
        var browser = new FakeGatewayTool("web.browse", ToolCapabilities.WebBrowser);
        var registry = new ToolRegistry([search, fetch, browser], new CapabilityAuthorizer());

        var available = registry.DescribeAvailable([ToolCapabilities.WebSearch, ToolCapabilities.WebRead]);

        Assert.Equal(2, available.Count);
        Assert.Contains(available, tool => tool.Name == "web.search");
        Assert.Contains(available, tool => tool.Name == "web.fetch");
        Assert.DoesNotContain(available, tool => tool.Name == "web.browse");
    }

    [Fact]
    public void ListAvailable_is_empty_when_caller_has_no_capabilities()
    {
        var registry = new ToolRegistry(
            [new FakeGatewayTool("web.search", ToolCapabilities.WebSearch)],
            new CapabilityAuthorizer());

        Assert.Empty(registry.ListAvailable([]));
    }

    [Fact]
    public void Registry_rejects_duplicate_names()
    {
        var tools = new ITool[]
        {
            new FakeGatewayTool("web.search", ToolCapabilities.WebSearch),
            new FakeGatewayTool("web.search", ToolCapabilities.WebSearch)
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ToolRegistry(tools, new CapabilityAuthorizer()));

        Assert.Contains("Duplicate tool name 'web.search'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Get_is_case_sensitive()
    {
        var registry = new ToolRegistry(
            [new FakeGatewayTool("web.search", ToolCapabilities.WebSearch)],
            new CapabilityAuthorizer());

        Assert.NotNull(registry.Get("web.search"));
        Assert.Null(registry.Get("WEB.SEARCH"));
    }
}
