namespace Memory.Application.Abstractions.AI;

using Memory.Application.Tools;

public sealed record ChatCompletionMessage(
    string Role,
    string Content,
    string? ToolCallId = null,
    string? Name = null,
    IReadOnlyList<ToolCall>? ToolCalls = null);
