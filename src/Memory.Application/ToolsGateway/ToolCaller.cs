namespace Memory.Application.ToolsGateway;

public sealed record ToolCaller(string Id, IReadOnlySet<string> Capabilities)
{
    public static ToolCaller Anonymous(IEnumerable<string>? capabilities = null)
    {
        return new("anonymous", ToolCapabilities.NormalizeAll(capabilities));
    }
}
