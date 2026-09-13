namespace Memory.Application.Tests.ToolsGateway;

using System.Text;
using Memory.Application.ToolsGateway.Web;

public sealed class HtmlContentCleanerTests
{
    [Fact]
    public void Strips_scripts_and_keeps_visible_text()
    {
        var html = Encoding.UTF8.GetBytes(
            """
            <html>
              <head><title>News</title><style>body{color:red}</style></head>
              <body>
                <script>window.track()</script>
                <h1>Headline</h1>
                <p>First paragraph.</p>
              </body>
            </html>
            """);

        var page = HtmlContentCleaner.Clean(
            "https://example.com/news",
            200,
            "text/html",
            html,
            maxTextChars: 5000);

        Assert.Equal("News", page.Title);
        Assert.Contains("Headline", page.Text, StringComparison.Ordinal);
        Assert.Contains("First paragraph.", page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("window.track", page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("color:red", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Binary_content_returns_empty_text()
    {
        var page = HtmlContentCleaner.Clean(
            "https://example.com/logo.png",
            200,
            "image/png",
            [1, 2, 3, 4],
            maxTextChars: 100);

        Assert.Equal(string.Empty, page.Text);
        Assert.Null(page.Title);
        Assert.Equal("image/png", page.ContentType);
    }
}
