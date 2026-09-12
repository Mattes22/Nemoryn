namespace Memory.Application.Tests.Tools;

using Memory.Application.Tools;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Tools;

public sealed class ToolRuntimeTests
{
    [Fact]
    public async Task Unknown_tool_returns_error_result()
    {
        var registry = new ToolRegistry([new GetTimeTool(TimeProvider.System)]);
        var runtime = TestToolRuntime.Create(registry);

        var result = await runtime.InvokeAsync(new ToolCall("call_1", "explode", "{}"), ToolContext.None);

        Assert.False(result.Ok);
        Assert.Equal("explode", result.Name);
        Assert.Equal("Unknown tool 'explode'.", result.Content);
    }

    [Fact]
    public async Task Oversized_arguments_are_rejected()
    {
        var registry = new ToolRegistry([new GetTimeTool(TimeProvider.System)]);
        var runtime = TestToolRuntime.Create(registry);
        var arguments = new string('a', ToolRuntime.MaxArgumentChars + 1);

        var result = await runtime.InvokeAsync(new ToolCall("call_1", "get_time", arguments), ToolContext.None);

        Assert.False(result.Ok);
        Assert.Equal("Tool arguments exceeded the size limit.", result.Content);
    }

    [Fact]
    public void Registry_rejects_duplicate_names()
    {
        var tools = new ITool[]
        {
            new GetTimeTool(TimeProvider.System),
            new GetTimeTool(new FixedTimeProvider(DateTimeOffset.UnixEpoch))
        };

        var exception = Assert.Throws<InvalidOperationException>(() => new ToolRegistry(tools));

        Assert.Contains("Duplicate tool name 'get_time'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Default_turn_allows_builtin_clock_and_memory_read()
    {
        var time = new FakeTool(
            new ToolDefinition("get_time", "clock", [], ToolTrust.Builtin, [ToolCapability.Clock]),
            "now");
        var memory = new FakeTool(
            new ToolDefinition("search_memories", "memory", [], ToolTrust.Builtin, [ToolCapability.MemoryRead]),
            "hits");
        var runtime = TestToolRuntime.Create(new ToolRegistry([time, memory]));

        var clock = await runtime.InvokeAsync(new ToolCall("c1", "get_time", "{}"), ToolContext.None);
        var search = await runtime.InvokeAsync(new ToolCall("c2", "search_memories", "{}"), ToolContext.None);

        Assert.True(clock.Ok);
        Assert.Equal("now", clock.Content);
        Assert.True(time.Invoked);
        Assert.True(search.Ok);
        Assert.Equal("hits", search.Content);
        Assert.True(memory.Invoked);
    }

    [Fact]
    public async Task Default_turn_denies_untrusted_tool()
    {
        var tool = new FakeTool(
            new ToolDefinition(
                "web_search",
                "untrusted",
                [],
                ToolTrust.Untrusted,
                [ToolCapability.Clock]));
        var runtime = TestToolRuntime.Create(new ToolRegistry([tool]));

        var result = await runtime.InvokeAsync(new ToolCall("call_1", "web_search", "{}"), ToolContext.None);

        Assert.False(result.Ok);
        Assert.Equal("Tool 'web_search' is not allowed: trust Untrusted.", result.Content);
        Assert.False(tool.Invoked);
    }

    [Fact]
    public async Task Default_turn_denies_network_filesystem_and_shell()
    {
        var network = new FakeTool(
            new ToolDefinition("http_get", "net", [], ToolTrust.Builtin, [ToolCapability.Network]));
        var files = new FakeTool(
            new ToolDefinition("read_file", "fs", [], ToolTrust.Builtin, [ToolCapability.FileSystem]));
        var shell = new FakeTool(
            new ToolDefinition("run_cmd", "sh", [], ToolTrust.Builtin, [ToolCapability.Shell]));
        var runtime = TestToolRuntime.Create(new ToolRegistry([network, files, shell]));

        var networkResult = await runtime.InvokeAsync(new ToolCall("n", "http_get", "{}"), ToolContext.None);
        var fileResult = await runtime.InvokeAsync(new ToolCall("f", "read_file", "{}"), ToolContext.None);
        var shellResult = await runtime.InvokeAsync(new ToolCall("s", "run_cmd", "{}"), ToolContext.None);

        Assert.Equal("Tool 'http_get' is not allowed: capability Network.", networkResult.Content);
        Assert.Equal("Tool 'read_file' is not allowed: capability FileSystem.", fileResult.Content);
        Assert.Equal("Tool 'run_cmd' is not allowed: capability Shell.", shellResult.Content);
        Assert.False(network.Invoked);
        Assert.False(files.Invoked);
        Assert.False(shell.Invoked);
    }

    [Fact]
    public async Task Explicit_turn_can_allow_network()
    {
        var tool = new FakeTool(
            new ToolDefinition("http_get", "net", [], ToolTrust.Builtin, [ToolCapability.Network]),
            "ok");
        var runtime = TestToolRuntime.Create(new ToolRegistry([tool]));
        var context = new ToolContext(
            Guid.Empty,
            string.Empty,
            Memory.Application.Configuration.MemoryPolicyKind.Balanced,
            ToolContext.DefaultAllowedTrusts,
            [ToolCapability.Clock, ToolCapability.MemoryRead, ToolCapability.Network]);

        var result = await runtime.InvokeAsync(new ToolCall("call_1", "http_get", "{}"), context);

        Assert.True(result.Ok);
        Assert.Equal("ok", result.Content);
        Assert.True(tool.Invoked);
    }

    [Fact]
    public async Task Owner_turn_records_success_deny_and_unknown_without_chat_messages()
    {
        var store = new FakeMemoryStore();
        var utc = new DateTimeOffset(2026, 9, 10, 20, 15, 0, TimeSpan.Zero);
        var clock = new FakeTool(
            new ToolDefinition("get_time", "clock", [], ToolTrust.Builtin, [ToolCapability.Clock]),
            "now");
        var untrusted = new FakeTool(
            new ToolDefinition(
                "web_search",
                "untrusted",
                [],
                ToolTrust.Untrusted,
                [ToolCapability.Network]));
        var runtime = TestToolRuntime.Create(
            new ToolRegistry([clock, untrusted]),
            store,
            new FixedTimeProvider(utc));
        var context = ToolContext.ForAgentTurn(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "user-1",
            Memory.Application.Configuration.MemoryPolicyKind.Balanced);

        var success = await runtime.InvokeAsync(new ToolCall("c1", "get_time", "{}"), context);
        var denied = await runtime.InvokeAsync(new ToolCall("c2", "web_search", "{}"), context);
        var unknown = await runtime.InvokeAsync(new ToolCall("c3", "explode", "{}"), context);

        Assert.True(success.Ok);
        Assert.False(denied.Ok);
        Assert.False(unknown.Ok);
        Assert.False(untrusted.Invoked);
        Assert.Equal(3, store.ToolAuditLogs.Count);

        var recordedSuccess = store.ToolAuditLogs[0];
        Assert.Equal("user-1", recordedSuccess.OwnerId);
        Assert.Equal(context.ConversationId, recordedSuccess.ConversationId);
        Assert.Equal("get_time", recordedSuccess.Name);
        Assert.Equal("Builtin", recordedSuccess.Trust);
        Assert.Equal(["Clock"], recordedSuccess.CapabilityNames);
        Assert.True(recordedSuccess.Ok);
        Assert.True(recordedSuccess.Invoked);
        Assert.Equal(ToolAuditOutcome.Succeeded, recordedSuccess.Outcome);
        Assert.Equal("now", recordedSuccess.Result);
        Assert.Null(recordedSuccess.Error);
        Assert.Equal(utc, recordedSuccess.OccurredAt);

        var recordedDenied = store.ToolAuditLogs[1];
        Assert.Equal("web_search", recordedDenied.Name);
        Assert.Equal("Untrusted", recordedDenied.Trust);
        Assert.Equal(["Network"], recordedDenied.CapabilityNames);
        Assert.False(recordedDenied.Ok);
        Assert.False(recordedDenied.Invoked);
        Assert.Equal(ToolAuditOutcome.Denied, recordedDenied.Outcome);
        Assert.Equal("Tool 'web_search' is not allowed: trust Untrusted.", recordedDenied.Error);

        var recordedUnknown = store.ToolAuditLogs[2];
        Assert.Equal("explode", recordedUnknown.Name);
        Assert.Null(recordedUnknown.Trust);
        Assert.Empty(recordedUnknown.CapabilityNames);
        Assert.False(recordedUnknown.Invoked);
        Assert.Equal(ToolAuditOutcome.Unknown, recordedUnknown.Outcome);
        Assert.Equal("Unknown tool 'explode'.", recordedUnknown.Error);
    }

    [Fact]
    public async Task Missing_owner_does_not_write_audit()
    {
        var store = new FakeMemoryStore();
        var runtime = TestToolRuntime.Create(
            [new FakeTool(new ToolDefinition("get_time", "clock", [], ToolTrust.Builtin, [ToolCapability.Clock]), "now")],
            store);

        await runtime.InvokeAsync(new ToolCall("call_1", "get_time", "{}"), ToolContext.None);

        Assert.Empty(store.ToolAuditLogs);
    }

    [Fact]
    public async Task NetworkOnce_allows_one_network_call_then_denies()
    {
        var store = new FakeMemoryStore();
        var network = new FakeTool(
            new ToolDefinition("http_get", "net", [], ToolTrust.Untrusted, [ToolCapability.Network]),
            "ok");
        var runtime = TestToolRuntime.Create(new ToolRegistry([network]), store);
        var context = ToolContext.ForAgentTurn(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "user-1",
            Memory.Application.Configuration.MemoryPolicyKind.Balanced,
            ToolPermissionProfile.NetworkOnce);

        var first = await runtime.InvokeAsync(new ToolCall("c1", "http_get", "{}"), context);
        var second = await runtime.InvokeAsync(new ToolCall("c2", "http_get", "{}"), context);

        Assert.True(first.Ok);
        Assert.Equal("ok", first.Content);
        Assert.False(second.Ok);
        Assert.Equal("Tool 'http_get' is not allowed: NetworkOnce already used.", second.Content);
        Assert.Equal(1, network.InvokedCount);
        Assert.Equal(2, store.ToolAuditLogs.Count);
        Assert.Equal(ToolAuditOutcome.Succeeded, store.ToolAuditLogs[0].Outcome);
        Assert.Equal(ToolAuditOutcome.Denied, store.ToolAuditLogs[1].Outcome);
        Assert.False(store.ToolAuditLogs[1].Invoked);
    }
}
