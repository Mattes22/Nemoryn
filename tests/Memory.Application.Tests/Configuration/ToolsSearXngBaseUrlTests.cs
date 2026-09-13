namespace Memory.Application.Tests.Configuration;

using Memory.Application.Configuration;

public sealed class ToolsSearXngBaseUrlTests
{
    [Fact]
    public void Normalize_strips_path_and_trailing_slash()
    {
        Assert.Equal("http://127.0.0.1:8080", ToolsSearXngBaseUrl.Normalize("http://127.0.0.1:8080/search"));
        Assert.Equal("http://host.docker.internal:8080", ToolsSearXngBaseUrl.Normalize("http://host.docker.internal:8080/"));
        Assert.Equal(string.Empty, ToolsSearXngBaseUrl.Normalize("  "));
    }

    [Fact]
    public void Normalize_rejects_relative_non_http_and_userinfo()
    {
        Assert.Throws<ArgumentException>(() => ToolsSearXngBaseUrl.Normalize("192.168.1.2:8080"));
        Assert.Throws<ArgumentException>(() => ToolsSearXngBaseUrl.Normalize("ftp://192.168.1.2:8080"));
        Assert.Throws<ArgumentException>(() => ToolsSearXngBaseUrl.Normalize("http://user:pass@127.0.0.1:8080"));
    }
}
