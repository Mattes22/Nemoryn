namespace Memory.Application.Tools;

using System.Text.Json;
using System.Text.Json.Serialization;
using Memory.Application.Abstractions.Persistence;
using Memory.Application.Memories;

internal sealed class SearchMemoriesTool(
    IMemoryStore memoryStore,
    IMemoryService memoryService) : ITool
{
    internal const int DefaultLimit = 5;
    internal const int MaxLimit = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public ToolDefinition Definition { get; } = new(
        "search_memories",
        "Searches this owner's memories by query. Read-only. Use when listed memories are not enough.",
        [
            new ToolParameter(
                "query",
                "string",
                "What to look up in memory, e.g. where the user lives.",
                Required: true),
            new ToolParameter(
                "limit",
                "integer",
                "Maximum matches to return. Defaults to 5, capped at 8.",
                Required: false)
        ],
        ToolTrust.Builtin,
        [ToolCapability.MemoryRead]);

    public async Task<ToolResult> InvokeAsync(
        ToolCall call,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.IsAvailable)
        {
            return Failed(call, "Tool context is required.");
        }

        string? query = null;
        int? limit = null;
        var argumentsJson = string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson.Trim();
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Failed(call, "Arguments must be a JSON object.");
            }

            if (document.RootElement.TryGetProperty("query", out var queryElement)
                && queryElement.ValueKind != JsonValueKind.Null
                && queryElement.ValueKind != JsonValueKind.Undefined)
            {
                if (queryElement.ValueKind != JsonValueKind.String)
                {
                    return Failed(call, "query must be a string.");
                }

                query = queryElement.GetString();
            }

            if (document.RootElement.TryGetProperty("limit", out var limitElement)
                && limitElement.ValueKind != JsonValueKind.Null
                && limitElement.ValueKind != JsonValueKind.Undefined)
            {
                if (!TryReadLimit(limitElement, out limit))
                {
                    return Failed(call, "limit must be a number.");
                }
            }
        }
        catch (JsonException)
        {
            return Failed(call, "Invalid JSON arguments.");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return Failed(call, "query is required.");
        }

        var conversation = await memoryStore.GetConversationAsync(context.ConversationId, cancellationToken);
        if (conversation is null)
        {
            return Failed(call, "Conversation was not found.");
        }

        if (!string.Equals(conversation.OwnerId, context.OwnerId.Trim(), StringComparison.Ordinal))
        {
            return Failed(call, "Tool context does not match the conversation owner.");
        }

        var matches = await memoryService.SearchAsync(
            context.ConversationId,
            new SearchMemoriesRequest(query.Trim(), NormalizeLimit(limit)),
            cancellationToken);

        var owned = matches
            .Where(match => string.Equals(match.Memory.OwnerId, context.OwnerId.Trim(), StringComparison.Ordinal))
            .Select(match => new SearchMemoryHit(
                match.Memory.Id,
                match.Memory.Type.ToString(),
                match.Memory.Content,
                Math.Round(match.Score, 4, MidpointRounding.AwayFromZero)))
            .ToArray();

        var payload = new SearchMemoriesPayload(
            query.Trim(),
            context.Policy.ToString(),
            owned.Length,
            owned);

        return new ToolResult(
            call.Id,
            Definition.Name,
            true,
            JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static int NormalizeLimit(int? limit)
    {
        if (limit is null || limit <= 0)
        {
            return DefaultLimit;
        }

        return Math.Min(limit.Value, MaxLimit);
    }

    private static bool TryReadLimit(JsonElement element, out int? limit)
    {
        limit = null;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var integer))
        {
            limit = integer;
            return true;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var number))
        {
            limit = (int)Math.Truncate(number);
            return true;
        }

        return false;
    }

    private ToolResult Failed(ToolCall call, string message)
        => new(call.Id, Definition.Name, false, message);

    private sealed record SearchMemoriesPayload(
        string Query,
        string Policy,
        int Count,
        IReadOnlyList<SearchMemoryHit> Memories);

    private sealed record SearchMemoryHit(
        Guid Id,
        string Type,
        string Content,
        double Score);
}
