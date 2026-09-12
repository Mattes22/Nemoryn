namespace Memory.Application.Abstractions.AI;

using Memory.Application.Tools;

public sealed record ChatCompletionResponse(
    string Content,
    IReadOnlyList<ToolCall>? ToolCalls = null)
{
    public bool HasToolCalls => ToolCalls is { Count: > 0 };
}
