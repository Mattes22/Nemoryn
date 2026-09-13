namespace Memory.Application.ToolsGateway;

using System.Text.Json;

public interface IToolExecutor
{
    Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        JsonElement arguments,
        ToolCaller caller,
        CancellationToken cancellationToken = default);
}
