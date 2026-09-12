namespace Memory.Infrastructure.AI;

using System.Text.Json.Serialization;

internal sealed record OllamaChatResponse(OllamaChatMessage? Message);

internal sealed record OllamaChatMessage(
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("thinking")] string? Thinking = null,
    [property: JsonPropertyName("reasoning")] string? Reasoning = null,
    [property: JsonPropertyName("tool_calls")] IReadOnlyList<OllamaToolCall>? ToolCalls = null);

internal sealed record OllamaToolCall(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("function")] OllamaToolFunction? Function);

internal sealed record OllamaToolFunction(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("arguments")] System.Text.Json.JsonElement Arguments);
