namespace Memory.Application.Tests.Fakes;

using Memory.Application.Tools;

internal sealed class FakeTool(ToolDefinition definition, string result = "ran") : ITool
{
    public bool Invoked { get; private set; }
    public int InvokedCount { get; private set; }

    public ToolDefinition Definition { get; } = definition;

    public Task<ToolResult> InvokeAsync(
        ToolCall call,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        Invoked = true;
        InvokedCount++;
        return Task.FromResult(new ToolResult(call.Id, Definition.Name, true, result));
    }
}
