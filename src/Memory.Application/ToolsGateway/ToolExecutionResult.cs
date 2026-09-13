namespace Memory.Application.ToolsGateway;

public sealed record ToolExecutionResult(
    string ToolName,
    ToolExecutionStatus Status,
    bool Allowed,
    bool Ok,
    object? Result,
    string? Error,
    int DurationMs)
{
    public bool Found => Status != ToolExecutionStatus.NotFound;
}
