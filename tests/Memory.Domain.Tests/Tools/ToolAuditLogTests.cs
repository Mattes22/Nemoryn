namespace Memory.Domain.Tests.Tools;

using Memory.Domain.Tools;

public sealed class ToolAuditLogTests
{
    [Fact]
    public void Constructor_requires_owner_and_known_outcome()
    {
        Assert.Throws<ArgumentException>(() =>
            new ToolAuditLog(" ", "call-1", "get_time", ToolAuditOutcome.Succeeded));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ToolAuditLog("user-1", "call-1", "get_time", (ToolAuditOutcome)0));
    }

    [Fact]
    public void Succeeded_keeps_result_and_clears_error()
    {
        var conversationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var entry = new ToolAuditLog(
            "user-1",
            "call-1",
            "search_memories",
            ToolAuditOutcome.Succeeded,
            conversationId,
            """{"query":"home"}""",
            "Builtin",
            ["MemoryRead", "Clock"],
            "[{ \"id\": \"1\" }]",
            "should be ignored",
            DateTimeOffset.UnixEpoch);

        Assert.Equal("user-1", entry.OwnerId);
        Assert.Equal(conversationId, entry.ConversationId);
        Assert.True(entry.Ok);
        Assert.True(entry.Invoked);
        Assert.Equal("[{ \"id\": \"1\" }]", entry.Result);
        Assert.Null(entry.Error);
        Assert.Equal("Builtin", entry.Trust);
        Assert.Equal(new[] { "MemoryRead", "Clock" }, entry.CapabilityNames);
        Assert.Equal(DateTimeOffset.UnixEpoch, entry.OccurredAt);
    }

    [Fact]
    public void Denied_is_not_invoked_and_keeps_error()
    {
        var entry = new ToolAuditLog(
            "user-1",
            "call-1",
            "echo_http",
            ToolAuditOutcome.Denied,
            Guid.Empty,
            "{}",
            "Untrusted",
            ["Network"],
            "ignored result",
            "Tool 'echo_http' is not allowed: trust Untrusted.");

        Assert.Null(entry.ConversationId);
        Assert.False(entry.Ok);
        Assert.False(entry.Invoked);
        Assert.Null(entry.Result);
        Assert.Equal("Tool 'echo_http' is not allowed: trust Untrusted.", entry.Error);
    }

    [Fact]
    public void Truncates_arguments_result_and_error()
    {
        var entry = new ToolAuditLog(
            "user-1",
            new string('c', ToolAuditLog.MaxCallIdLength + 8),
            new string('n', ToolAuditLog.MaxNameLength + 8),
            ToolAuditOutcome.Failed,
            argumentsJson: new string('a', ToolAuditLog.MaxArgumentsLength + 8),
            result: new string('r', ToolAuditLog.MaxResultLength + 8),
            error: new string('e', ToolAuditLog.MaxErrorLength + 8));

        Assert.Equal(ToolAuditLog.MaxCallIdLength, entry.CallId.Length);
        Assert.Equal(ToolAuditLog.MaxNameLength, entry.Name.Length);
        Assert.Equal(ToolAuditLog.MaxArgumentsLength, entry.ArgumentsJson?.Length);
        Assert.Null(entry.Result);
        Assert.Equal(ToolAuditLog.MaxErrorLength, entry.Error?.Length);
        Assert.True(entry.Invoked);
        Assert.False(entry.Ok);
    }
}
