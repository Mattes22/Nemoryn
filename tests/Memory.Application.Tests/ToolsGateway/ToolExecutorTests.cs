namespace Memory.Application.Tests.ToolsGateway;

using System.Text.Json;
using Memory.Application.ToolsGateway;
using Memory.Application.Tests.Fakes;

public sealed class ToolExecutorTests
{
    [Fact]
    public async Task Unknown_tool_is_not_found_and_is_audited()
    {
        var auditor = new RecordingToolInvocationAuditor();
        var executor = CreateExecutor([new FakeGatewayTool("web.search", ToolCapabilities.WebSearch)], auditor);

        var result = await executor.ExecuteAsync(
            "missing",
            JsonDocument.Parse("{}").RootElement,
            new ToolCaller("owui:anon", ToolCapabilities.NormalizeAll([ToolCapabilities.WebSearch])));

        Assert.Equal(ToolExecutionStatus.NotFound, result.Status);
        Assert.False(result.Ok);
        Assert.False(result.Allowed);
        Assert.Equal("Unknown tool 'missing'.", result.Error);
        var audit = Assert.Single(auditor.Entries);
        Assert.Equal(ToolExecutionStatus.NotFound, audit.Status);
        Assert.Equal("owui:anon", audit.CallerId);
        Assert.Equal("missing", audit.ToolName);
    }

    [Fact]
    public async Task Denied_tool_is_not_invoked()
    {
        var tool = new FakeGatewayTool("web.search", ToolCapabilities.WebSearch);
        var auditor = new RecordingToolInvocationAuditor();
        var executor = CreateExecutor([tool], auditor);

        var result = await executor.ExecuteAsync(
            "web.search",
            JsonDocument.Parse("{}").RootElement,
            new ToolCaller("owui:user-1", ToolCapabilities.NormalizeAll([ToolCapabilities.WebRead])));

        Assert.Equal(ToolExecutionStatus.Denied, result.Status);
        Assert.False(result.Allowed);
        Assert.False(tool.Invoked);
        var audit = Assert.Single(auditor.Entries);
        Assert.Equal("owui:user-1", audit.CallerId);
        Assert.Equal("web.search", audit.ToolName);
        Assert.False(audit.Allowed);
        Assert.False(audit.Ok);
    }

    [Fact]
    public async Task Authorized_tool_returns_payload_without_page_bodies_in_audit()
    {
        var tool = new FakeGatewayTool("web.fetch", ToolCapabilities.WebRead)
        {
            ExecutePayload = new Dictionary<string, string> { ["text"] = new string('x', 5000) }
        };
        var auditor = new RecordingToolInvocationAuditor();
        var executor = CreateExecutor([tool], auditor);

        var result = await executor.ExecuteAsync(
            "web.fetch",
            JsonDocument.Parse("""{"url":"https://example.com"}""").RootElement,
            new ToolCaller("cursor", ToolCapabilities.NormalizeAll([ToolCapabilities.WebRead])));

        Assert.True(result.Ok);
        Assert.True(result.Allowed);
        Assert.Equal(ToolExecutionStatus.Succeeded, result.Status);
        Assert.True(tool.Invoked);
        var audit = Assert.Single(auditor.Entries);
        Assert.Null(audit.Error);
        Assert.True(audit.Ok);
        Assert.DoesNotContain("xxxxx", audit.CallerId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unsafe_url_maps_to_forbidden_url_status()
    {
        var tool = new FakeGatewayTool("web.fetch", ToolCapabilities.WebRead)
        {
            ExecuteException = new UnsafeUrlException("URL is not allowed.")
        };
        var executor = CreateExecutor([tool], new RecordingToolInvocationAuditor());

        var result = await executor.ExecuteAsync(
            "web.fetch",
            JsonDocument.Parse("{}").RootElement,
            ToolCaller.Anonymous([ToolCapabilities.WebRead]));

        Assert.Equal(ToolExecutionStatus.ForbiddenUrl, result.Status);
        Assert.True(result.Allowed);
        Assert.False(result.Ok);
        Assert.Equal("URL is not allowed.", result.Error);
    }

    private static ToolExecutor CreateExecutor(IEnumerable<ITool> tools, IToolInvocationAuditor auditor)
    {
        var authorizer = new CapabilityAuthorizer();
        return new ToolExecutor(
            new ToolRegistry(tools, authorizer),
            authorizer,
            auditor,
            TimeProvider.System);
    }
}
