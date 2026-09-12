namespace Memory.Application.Tools;

public sealed record ToolDefinition(
    string Name,
    string Description,
    IReadOnlyList<ToolParameter> Parameters,
    ToolTrust Trust,
    IReadOnlyList<ToolCapability> Capabilities);
