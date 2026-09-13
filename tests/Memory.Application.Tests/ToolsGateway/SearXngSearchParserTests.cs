namespace Memory.Application.Tests.ToolsGateway;

using Memory.Application.ToolsGateway.Web;

public sealed class SearXngSearchParserTests
{
    [Fact]
    public void Parse_maps_title_url_snippet_engine_and_score()
    {
        const string json = """
            {
              "query": "nemoryn",
              "results": [
                {
                  "url": "https://example.com/a",
                  "title": "Example A",
                  "content": "First snippet",
                  "engine": "duckduckgo",
                  "score": 1.5
                },
                {
                  "url": "https://example.org/b",
                  "title": "Example B",
                  "content": "Second snippet",
                  "engines": ["wikipedia"],
                  "score": "0.25"
                }
              ]
            }
            """;

        var result = SearXngSearchParser.Parse(json, "nemoryn", maxResults: 10);

        Assert.Equal("nemoryn", result.Query);
        Assert.Equal("SearXNG", result.Provider);
        Assert.Equal(2, result.Results.Count);
        Assert.Equal("Example A", result.Results[0].Title);
        Assert.Equal("https://example.com/a", result.Results[0].Url);
        Assert.Equal("First snippet", result.Results[0].Snippet);
        Assert.Equal("duckduckgo", result.Results[0].Source);
        Assert.Equal(1.5, result.Results[0].Score);
        Assert.Equal("wikipedia", result.Results[1].Source);
        Assert.Equal(0.25, result.Results[1].Score);
    }

    [Fact]
    public void Parse_skips_non_http_urls_and_respects_max_results()
    {
        const string json = """
            {
              "results": [
                { "url": "file:///etc/passwd", "title": "bad", "content": "x", "engine": "x" },
                { "url": "https://example.com/1", "title": "One", "content": "a", "engine": "google" },
                { "url": "https://example.com/2", "title": "Two", "content": "b", "engine": "google" },
                { "title": "Missing URL", "content": "c", "engine": "google" }
              ]
            }
            """;

        var result = SearXngSearchParser.Parse(json, "q", maxResults: 1);

        var hit = Assert.Single(result.Results);
        Assert.Equal("https://example.com/1", hit.Url);
        Assert.Equal("One", hit.Title);
    }

    [Fact]
    public void Parse_empty_results_returns_empty_list()
    {
        var result = SearXngSearchParser.Parse("""{"results":[]}""", "empty", 5);

        Assert.Equal("empty", result.Query);
        Assert.Empty(result.Results);
    }
}
