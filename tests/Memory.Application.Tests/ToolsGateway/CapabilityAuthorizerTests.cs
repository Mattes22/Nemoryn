namespace Memory.Application.Tests.ToolsGateway;

using Memory.Application.ToolsGateway;
using Memory.Application.Tests.Fakes;

public sealed class CapabilityAuthorizerTests
{
    private readonly CapabilityAuthorizer _authorizer = new();

    [Fact]
    public void Grants_tool_when_all_required_capabilities_are_present()
    {
        var tool = new FakeGatewayTool("web.search", ToolCapabilities.WebSearch);

        Assert.True(_authorizer.IsAuthorized(tool, [ToolCapabilities.WebSearch, ToolCapabilities.WebRead]));
    }

    [Fact]
    public void Capability_names_are_case_insensitive()
    {
        var tool = new FakeGatewayTool("web.search", "WEB.SEARCH");

        Assert.True(_authorizer.IsAuthorized(tool, ["Web.Search"]));
    }

    [Fact]
    public void Denies_tool_when_a_required_capability_is_missing()
    {
        var tool = new FakeGatewayTool("web.fetch", ToolCapabilities.WebRead);

        Assert.False(_authorizer.IsAuthorized(tool, [ToolCapabilities.WebSearch]));
        Assert.False(_authorizer.IsAuthorized(tool, [ToolCapabilities.NetworkLocal]));
    }

    [Fact]
    public void Web_fetch_does_not_use_network_local()
    {
        var tool = new FakeGatewayTool("web.fetch", ToolCapabilities.WebRead);

        Assert.True(_authorizer.IsAuthorized(tool, [ToolCapabilities.WebRead]));
        Assert.DoesNotContain(ToolCapabilities.NetworkLocal, tool.RequiredCapabilities);
    }

    [Fact]
    public void Known_capability_catalog_includes_future_slots()
    {
        Assert.Contains(ToolCapabilities.MemoryRead, ToolCapabilities.All);
        Assert.Contains(ToolCapabilities.MemoryWrite, ToolCapabilities.All);
        Assert.Contains(ToolCapabilities.WebSearch, ToolCapabilities.All);
        Assert.Contains(ToolCapabilities.WebRead, ToolCapabilities.All);
        Assert.Contains(ToolCapabilities.WebBrowser, ToolCapabilities.All);
        Assert.Contains(ToolCapabilities.FilesystemRead, ToolCapabilities.All);
        Assert.Contains(ToolCapabilities.FilesystemWrite, ToolCapabilities.All);
        Assert.Contains(ToolCapabilities.NetworkLocal, ToolCapabilities.All);
        Assert.Contains(ToolCapabilities.ShellExecute, ToolCapabilities.All);
    }
}
