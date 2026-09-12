namespace Memory.Application.Tools;

public interface IToolRuntime
{
    Task<ToolResult> InvokeAsync(
        ToolCall call,
        ToolContext context,
        CancellationToken cancellationToken = default);
}
