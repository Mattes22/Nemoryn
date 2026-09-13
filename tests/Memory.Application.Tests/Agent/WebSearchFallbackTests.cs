namespace Memory.Application.Tests.Agent;

using Memory.Application.Agent;
using Memory.Application.Tools;

public sealed class WebSearchFallbackTests
{
    [Theory]
    [InlineData("Najdi mi informace o Teuta Ganna", "Teuta Ganna")]
    [InlineData("kdo je Ada Lovelace?", "Ada Lovelace")]
    [InlineData("Look up CERN", "CERN")]
    public void ExtractQuery_strips_lookup_prefixes(string message, string expected)
    {
        Assert.Equal(expected, WebSearchFallback.ExtractQuery(message));
    }

    [Theory]
    [InlineData("Najdi mi informace o Teuta Ganna", true)]
    [InlineData("Hledej to na webu.", true)]
    [InlineData("Kolik je hodin?", false)]
    [InlineData("Kde bydlím?", false)]
    [InlineData("Ahoj", false)]
    public void LooksLikeLookup_detects_web_requests(string message, bool expected)
    {
        Assert.Equal(expected, WebSearchFallback.LooksLikeLookup(message));
    }

    [Fact]
    public void TryCreateCall_requires_advertised_web_search()
    {
        Assert.False(
            WebSearchFallback.TryCreateCall(
                [new ToolDefinition("get_time", "clock", [], ToolTrust.Builtin, [ToolCapability.Clock])],
                "Najdi mi informace o Teuta Ganna",
                out _));
        Assert.True(
            WebSearchFallback.TryCreateCall(
                [new ToolDefinition(
                    WebSearchAgentTool.ToolName,
                    "search",
                    [],
                    ToolTrust.Builtin,
                    [ToolCapability.WebSearch])],
                "Najdi mi informace o Teuta Ganna",
                out var call));
        Assert.Equal(WebSearchFallback.CallId, call.Id);
        Assert.Equal(WebSearchAgentTool.ToolName, call.Name);
        Assert.Contains("Teuta Ganna", call.ArgumentsJson, StringComparison.Ordinal);
    }
}
