namespace Memory.Application.Tests.Tools;

using System.Text.Json;
using Memory.Application.Tools;
using Memory.Application.Tests.Fakes;

public sealed class GetTimeToolTests
{
    [Fact]
    public async Task Get_time_returns_utc_iso_when_timezone_is_omitted()
    {
        var utc = new DateTimeOffset(2026, 9, 10, 18, 45, 0, TimeSpan.Zero);
        var tool = new GetTimeTool(new FixedTimeProvider(utc));

        var result = await tool.InvokeAsync(new ToolCall("call_1", "get_time", "{}"), ToolContext.None);

        Assert.True(result.Ok);
        using var document = JsonDocument.Parse(result.Content);
        Assert.Equal("UTC", document.RootElement.GetProperty("timezone").GetString());
        Assert.Equal("2026-09-10T18:45:00.0000000+00:00", document.RootElement.GetProperty("utc").GetString());
    }

    [Fact]
    public async Task Get_time_rejects_unknown_timezone()
    {
        var tool = new GetTimeTool(new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var result = await tool.InvokeAsync(
            new ToolCall("call_1", "get_time", """{"timezone":"Not/AZone"}"""),
            ToolContext.None);

        Assert.False(result.Ok);
        Assert.Contains("Unknown timezone", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_time_rejects_invalid_json()
    {
        var tool = new GetTimeTool(new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var result = await tool.InvokeAsync(new ToolCall("call_1", "get_time", "{"), ToolContext.None);

        Assert.False(result.Ok);
        Assert.Equal("Invalid JSON arguments.", result.Content);
    }
}
