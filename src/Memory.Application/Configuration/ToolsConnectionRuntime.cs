namespace Memory.Application.Configuration;

using Memory.Application.Runtime;

public sealed class ToolsConnectionRuntime
{
    public bool HasSearXngBaseUrlOverride { get; set; }

    public string SearXngBaseUrl { get; set; } = string.Empty;

    public void ApplyTo(ToolsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!HasSearXngBaseUrlOverride)
        {
            return;
        }

        options.Web.SearXNG.BaseUrl = SearXngBaseUrl;
    }

    public static ToolsConnectionRuntime FromStore(IToolsConnectionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        var runtime = new ToolsConnectionRuntime();
        var loaded = store.Load();
        if (loaded is null)
        {
            return runtime;
        }

        runtime.HasSearXngBaseUrlOverride = true;
        runtime.SearXngBaseUrl = loaded.SearXngBaseUrl ?? string.Empty;
        return runtime;
    }
}
