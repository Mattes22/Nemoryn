namespace Memory.Application.ToolsGateway;

public static class ToolCapabilities
{
    public const string MemoryRead = "memory.read";
    public const string MemoryWrite = "memory.write";
    public const string WebSearch = "web.search";
    public const string WebRead = "web.read";
    public const string WebBrowser = "web.browser";
    public const string FilesystemRead = "filesystem.read";
    public const string FilesystemWrite = "filesystem.write";
    public const string NetworkLocal = "network.local";
    public const string ShellExecute = "shell.execute";

    public static IReadOnlyList<string> All { get; } =
    [
        MemoryRead,
        MemoryWrite,
        WebSearch,
        WebRead,
        WebBrowser,
        FilesystemRead,
        FilesystemWrite,
        NetworkLocal,
        ShellExecute
    ];

    public static string Normalize(string? capability)
    {
        return string.IsNullOrWhiteSpace(capability)
            ? string.Empty
            : capability.Trim().ToLowerInvariant();
    }

    public static IReadOnlySet<string> NormalizeAll(IEnumerable<string>? capabilities)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var capability in capabilities ?? [])
        {
            var normalized = Normalize(capability);
            if (normalized.Length == 0)
            {
                continue;
            }

            set.Add(normalized);
        }

        return set;
    }
}
