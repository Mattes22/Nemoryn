namespace Memory.Application.Configuration;

using Memory.Application.ToolsGateway;

public sealed class ToolsOptions
{
    public const string SectionName = "Tools";

    public List<string> DefaultCapabilities { get; set; } =
    [
        ToolCapabilities.WebSearch,
        ToolCapabilities.WebRead
    ];

    public ToolsWebOptions Web { get; set; } = new();
}

public sealed class ToolsWebOptions
{
    public string SearchProvider { get; set; } = "SearXNG";

    public SearXngOptions SearXNG { get; set; } = new();

    public WebFetchOptions Fetch { get; set; } = new();
}

public sealed class SearXngOptions
{
    public string BaseUrl { get; set; } = string.Empty;
}

public sealed class WebFetchOptions
{
    public const int DefaultTimeoutSeconds = 10;
    public const int DefaultMaxResponseBytes = 1_048_576;
    public const int DefaultMaxRedirects = 5;
    public const int DefaultMaxTextChars = 50_000;

    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;
    public int MaxResponseBytes { get; set; } = DefaultMaxResponseBytes;
    public int MaxRedirects { get; set; } = DefaultMaxRedirects;
    public int MaxTextChars { get; set; } = DefaultMaxTextChars;

    public TimeSpan Timeout => TimeSpan.FromSeconds(Math.Clamp(TimeoutSeconds, 1, 60));

    public int NormalizedMaxResponseBytes => Math.Clamp(MaxResponseBytes, 1024, 5_242_880);

    public int NormalizedMaxRedirects => Math.Clamp(MaxRedirects, 0, 10);

    public int NormalizedMaxTextChars => Math.Clamp(MaxTextChars, 256, 200_000);
}
