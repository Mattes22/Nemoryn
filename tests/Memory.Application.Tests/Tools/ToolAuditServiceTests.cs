namespace Memory.Application.Tests.Tools;

using Memory.Application.Tools;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Tools;

public sealed class ToolAuditServiceTests
{
    [Fact]
    public async Task Get_returns_owner_scoped_entries_newest_first()
    {
        var store = new FakeMemoryStore();
        var conversation = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var other = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var older = DateTimeOffset.Parse("2026-09-10T18:00:00Z");
        var newer = DateTimeOffset.Parse("2026-09-10T19:00:00Z");
        store.AddToolAuditLog(new ToolAuditLog(
            "user-1",
            "call-old",
            "get_time",
            ToolAuditOutcome.Succeeded,
            conversation,
            "{}",
            "Builtin",
            ["Clock"],
            "old",
            occurredAt: older));
        store.AddToolAuditLog(new ToolAuditLog(
            "user-1",
            "call-new",
            "search_memories",
            ToolAuditOutcome.Succeeded,
            conversation,
            """{"query":"home"}""",
            "Builtin",
            ["MemoryRead"],
            "new",
            occurredAt: newer));
        store.AddToolAuditLog(new ToolAuditLog(
            "user-2",
            "call-other",
            "get_time",
            ToolAuditOutcome.Succeeded,
            other,
            "{}",
            "Builtin",
            ["Clock"],
            "secret"));

        var audit = new ToolAuditService(store, TimeProvider.System);
        var entries = await audit.GetForOwnerAsync("user-1");

        Assert.Equal(2, entries.Count);
        Assert.Equal("call-new", entries[0].CallId);
        Assert.Equal("call-old", entries[1].CallId);
        Assert.DoesNotContain(entries, entry => entry.OwnerId == "user-2");
    }

    [Fact]
    public async Task Get_can_filter_by_conversation()
    {
        var store = new FakeMemoryStore();
        var first = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var second = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        store.AddToolAuditLog(new ToolAuditLog(
            "user-1", "c1", "get_time", ToolAuditOutcome.Succeeded, first, "{}", "Builtin", ["Clock"], "a"));
        store.AddToolAuditLog(new ToolAuditLog(
            "user-1", "c2", "get_time", ToolAuditOutcome.Succeeded, second, "{}", "Builtin", ["Clock"], "b"));

        var audit = new ToolAuditService(store, TimeProvider.System);
        var entries = await audit.GetForOwnerAsync("user-1", second);

        var entry = Assert.Single(entries);
        Assert.Equal("c2", entry.CallId);
        Assert.Equal(second, entry.ConversationId);
    }

    [Fact]
    public async Task Record_skips_when_owner_is_missing()
    {
        var store = new FakeMemoryStore();
        var audit = new ToolAuditService(store, TimeProvider.System);

        await audit.RecordAsync(
            ToolContext.None,
            new ToolCall("call_1", "get_time", "{}"),
            new ToolDefinition("get_time", "clock", [], ToolTrust.Builtin, [ToolCapability.Clock]),
            ToolAuditOutcome.Succeeded,
            new ToolResult("call_1", "get_time", true, "now"));

        Assert.Empty(store.ToolAuditLogs);
    }
}
