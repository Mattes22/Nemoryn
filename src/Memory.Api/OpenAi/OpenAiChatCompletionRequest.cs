namespace Memory.Api.OpenAi;

using System.Text.Json.Serialization;

public sealed record OpenAiChatCompletionRequest(
    string? Model,
    IReadOnlyList<OpenAiChatMessage>? Messages,
    bool? Stream,
    string? User,
    [property: JsonPropertyName("conversation_id")] string? ConversationId,
    OpenAiChatMetadata? Metadata);

public sealed record OpenAiChatMessage(
    string Role,
    string? Content);

public sealed record OpenAiChatMetadata(
    [property: JsonPropertyName("owner_id")] string? OwnerId,
    [property: JsonPropertyName("conversation_id")] string? ConversationId,
    [property: JsonPropertyName("chat_id")] string? ChatId,
    string? Task);
