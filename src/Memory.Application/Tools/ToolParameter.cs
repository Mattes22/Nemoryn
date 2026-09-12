namespace Memory.Application.Tools;

public sealed record ToolParameter(
    string Name,
    string Type,
    string Description,
    bool Required = false);
