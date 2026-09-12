namespace Memory.Application.Tests.Tools;

using System.Net;
using Memory.Application.Configuration;
using Memory.Application.Tools;
using Memory.Application.Tests.Fakes;

public sealed class HttpToolTests
{
    [Fact]
    public void Manifest_is_always_untrusted_network_and_never_memory_read()
    {
        var tool = CreateTool(new ExternalToolDefinition
        {
            Name = "echo_http",
            Description = "Echo",
            Url = "https://example.com/tools/echo",
            Capabilities = ["MemoryRead", "Network", "Clock"]
        });

        Assert.Equal(ToolTrust.Untrusted, tool.Definition.Trust);
        Assert.Contains(ToolCapability.Network, tool.Definition.Capabilities);
        Assert.Contains(ToolCapability.Clock, tool.Definition.Capabilities);
        Assert.DoesNotContain(ToolCapability.MemoryRead, tool.Definition.Capabilities);
    }

    [Fact]
    public void Manifest_rejects_builtin_name_and_non_http_url()
    {
        var builtin = Assert.Throws<InvalidOperationException>(() =>
            HttpTool.Create(new ExternalToolDefinition
            {
                Name = "search_memories",
                Url = "https://example.com/tools/echo"
            }, new HttpClient()));
        var fileUrl = Assert.Throws<InvalidOperationException>(() =>
            HttpTool.Create(new ExternalToolDefinition
            {
                Name = "echo_http",
                Url = "file:///etc/passwd"
            }, new HttpClient()));

        Assert.Contains("collides with a built-in tool", builtin.Message, StringComparison.Ordinal);
        Assert.Contains("absolute http(s) URL", fileUrl.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invoke_posts_arguments_without_owner_or_memory_context()
    {
        var handler = new StubHttpHandler { ResponseBody = """{"pong":true}""" };
        var tool = CreateTool(
            new ExternalToolDefinition
            {
                Name = "echo_http",
                Description = "Echo",
                Url = "https://example.com/tools/echo"
            },
            handler);
        var context = new ToolContext(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "matej",
            MemoryPolicyKind.Balanced,
            [ToolTrust.Untrusted],
            [ToolCapability.Network]);

        var result = await tool.InvokeAsync(
            new ToolCall("call_1", "echo_http", """{"text":"hi"}"""),
            context);

        Assert.True(result.Ok);
        Assert.Equal("""{"pong":true}""", result.Content);
        Assert.True(handler.Invoked);
        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("https://example.com/tools/echo", handler.LastRequest?.RequestUri?.ToString());
        Assert.Equal("""{"text":"hi"}""", handler.LastBody);
        Assert.DoesNotContain("matej", handler.LastBody, StringComparison.Ordinal);
        Assert.DoesNotContain("aaaaaaaa", handler.LastBody, StringComparison.Ordinal);
        Assert.False(handler.LastRequest?.Headers.Contains("X-Owner-Id") == true);
    }

    [Fact]
    public async Task Invoke_returns_http_error_without_throwing()
    {
        var handler = new StubHttpHandler
        {
            Status = HttpStatusCode.BadGateway,
            ResponseBody = "down"
        };
        var tool = CreateTool(
            new ExternalToolDefinition
            {
                Name = "echo_http",
                Url = "https://example.com/tools/echo"
            },
            handler);

        var result = await tool.InvokeAsync(
            new ToolCall("call_1", "echo_http", "{}"),
            AllowedNetworkContext());

        Assert.False(result.Ok);
        Assert.Equal("HTTP 502: down", result.Content);
    }

    [Fact]
    public async Task Default_turn_does_not_invoke_http_tool()
    {
        var handler = new StubHttpHandler();
        var tool = CreateTool(
            new ExternalToolDefinition
            {
                Name = "echo_http",
                Url = "https://example.com/tools/echo"
            },
            handler);
        var runtime = TestToolRuntime.Create(new ToolRegistry([tool]));

        var result = await runtime.InvokeAsync(
            new ToolCall("call_1", "echo_http", "{}"),
            ToolContext.None);

        Assert.False(result.Ok);
        Assert.Equal("Tool 'echo_http' is not allowed: trust Untrusted.", result.Content);
        Assert.False(handler.Invoked);
    }

    [Fact]
    public async Task Explicit_untrusted_network_turn_invokes_http_tool()
    {
        var handler = new StubHttpHandler { ResponseBody = "ok" };
        var tool = CreateTool(
            new ExternalToolDefinition
            {
                Name = "echo_http",
                Url = "https://example.com/tools/echo"
            },
            handler);
        var runtime = TestToolRuntime.Create(new ToolRegistry([tool]));

        var result = await runtime.InvokeAsync(
            new ToolCall("call_1", "echo_http", "{}"),
            AllowedNetworkContext());

        Assert.True(result.Ok);
        Assert.Equal("ok", result.Content);
        Assert.True(handler.Invoked);
    }

    private static HttpTool CreateTool(ExternalToolDefinition definition, StubHttpHandler? handler = null)
    {
        handler ??= new StubHttpHandler();
        return HttpTool.Create(definition, new HttpClient(handler, disposeHandler: false));
    }

    private static ToolContext AllowedNetworkContext()
    {
        return new ToolContext(
            Guid.Empty,
            "user-1",
            MemoryPolicyKind.Balanced,
            [ToolTrust.Untrusted],
            [ToolCapability.Network]);
    }
}
