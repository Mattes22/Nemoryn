namespace Memory.Application.ToolsGateway;

public sealed record ToolParameterDescriptor(
    string Name,
    string Type,
    string Description,
    bool Required = false);
