namespace Memory.Application.Tests.Configuration;

using Memory.Application.Configuration;

public sealed class MemoryAiBaseUrlTests
{
    [Fact]
    public void Normalize_keeps_openai_v1_path_and_strips_trailing_slash()
    {
        Assert.Equal("https://api.openai.com/v1", MemoryAiBaseUrl.Normalize("https://api.openai.com/v1/"));
        Assert.Equal("http://192.168.1.2:11434", MemoryAiBaseUrl.Normalize("http://192.168.1.2:11434/"));
    }

    [Fact]
    public void Normalize_rejects_relative_non_http_and_userinfo()
    {
        Assert.Throws<ArgumentException>(() => MemoryAiBaseUrl.Normalize("192.168.1.2:11434"));
        Assert.Throws<ArgumentException>(() => MemoryAiBaseUrl.Normalize("ftp://192.168.1.2:11434"));
        Assert.Throws<ArgumentException>(() => MemoryAiBaseUrl.Normalize("http://user:pass@192.168.1.2:11434"));
        Assert.Throws<ArgumentException>(() => MemoryAiBaseUrl.Normalize(" "));
    }
}
