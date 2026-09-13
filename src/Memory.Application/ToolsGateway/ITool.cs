namespace Memory.Application.ToolsGateway;

using System.Text.Json;

public interface ITool
{
    string Name { get; }

    string Description { get; }

    IReadOnlyCollection<string> RequiredCapabilities { get; }

    IReadOnlyList<ToolParameterDescriptor> Parameters { get; }

    Task<object> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken = default);
}
