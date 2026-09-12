namespace Memory.Api.OpenAi;

using System.Text.Json.Serialization;

public sealed record OpenAiChatCompletionResponse(
    string Id,
    [property: JsonPropertyName("object")] string Object,
    long Created,
    string Model,
    IReadOnlyList<OpenAiChatChoice> Choices,
    OpenAiUsage Usage,
    [property: JsonPropertyName("memory")] OpenAiMemoryMetadata Memory);

public sealed record OpenAiChatChoice(
    int Index,
    OpenAiChatMessage Message,
    [property: JsonPropertyName("finish_reason")] string FinishReason);

public sealed record OpenAiUsage(
    [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
    [property: JsonPropertyName("completion_tokens")] int CompletionTokens,
    [property: JsonPropertyName("total_tokens")] int TotalTokens);

public sealed record OpenAiMemoryMetadata(
    string Status,
    string Provider,
    string Model,
    [property: JsonPropertyName("conversation_id")] Guid ConversationId,
    [property: JsonPropertyName("user_message_id")] Guid UserMessageId,
    [property: JsonPropertyName("assistant_message_id")] Guid? AssistantMessageId,
    [property: JsonPropertyName("core_count")] int CoreCount,
    [property: JsonPropertyName("relevant_count")] int RelevantCount,
    string? Error);

public sealed record OpenAiModelListResponse(
    [property: JsonPropertyName("object")] string Object,
    IReadOnlyList<OpenAiModelResponse> Data);

public sealed record OpenAiModelResponse(
    string Id,
    [property: JsonPropertyName("object")] string Object,
    long Created,
    [property: JsonPropertyName("owned_by")] string OwnedBy);

public sealed record OpenAiErrorResponse(OpenAiError Error);

public sealed record OpenAiError(
    string Message,
    string Type);

public sealed record OpenAiChatCompletionChunk(
    string Id,
    [property: JsonPropertyName("object")] string Object,
    long Created,
    string Model,
    IReadOnlyList<OpenAiChatStreamChoice> Choices);

public sealed record OpenAiChatStreamChoice(
    int Index,
    OpenAiChatDelta Delta,
    [property: JsonPropertyName("finish_reason")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? FinishReason);

public sealed record OpenAiChatDelta(
    string? Role,
    string? Content);
