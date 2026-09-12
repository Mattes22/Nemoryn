namespace Memory.Application.Tools;

public interface ITool
{
    ToolDefinition Definition { get; }

    Task<ToolResult> InvokeAsync(
        ToolCall call,
        ToolContext context,
        CancellationToken cancellationToken = default);
}
