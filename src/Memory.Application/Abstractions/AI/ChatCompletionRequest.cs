namespace Memory.Application.Abstractions.AI;

using Memory.Application.Tools;

public sealed record ChatCompletionRequest(
    IReadOnlyList<ChatCompletionMessage> Messages,
    IReadOnlyList<ToolDefinition>? Tools = null,
    string? Model = null);
