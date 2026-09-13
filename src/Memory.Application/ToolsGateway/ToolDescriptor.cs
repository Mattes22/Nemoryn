namespace Memory.Application.ToolsGateway;

public sealed record ToolDescriptor(
    string Name,
    string Description,
    IReadOnlyList<string> RequiredCapabilities,
    IReadOnlyList<ToolParameterDescriptor> Parameters);
