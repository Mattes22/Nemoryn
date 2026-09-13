namespace Memory.Application.Runtime;

public sealed record ToolsConnectionSettings(string SearXngBaseUrl);

public interface IToolsConnectionStore
{
    ToolsConnectionSettings? Load();
    bool TrySave(ToolsConnectionSettings settings, out string? error);
}
