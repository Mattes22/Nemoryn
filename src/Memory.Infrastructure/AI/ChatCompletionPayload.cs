namespace Memory.Infrastructure.AI;

using System.Text.Json;
using Memory.Application.Abstractions.AI;
using Memory.Application.Tools;

internal static class ChatCompletionPayload
{
    public static Dictionary<string, object?> ForOllama(
        string model,
        bool stream,
        ChatCompletionRequest request)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["stream"] = stream,
            ["messages"] = request.Messages.Select(ToOllamaMessage).ToArray()
        };
        if (request.Tools is { Count: > 0 })
        {
            body["tools"] = request.Tools.Select(ToFunctionTool).ToArray();
        }

        return body;
    }

    public static Dictionary<string, object?> ForOpenAi(
        string model,
        bool stream,
        ChatCompletionRequest request)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["temperature"] = 0.3,
            ["messages"] = request.Messages.Select(ToOpenAiMessage).ToArray()
        };
        if (stream)
        {
            body["stream"] = true;
        }

        if (request.Tools is { Count: > 0 })
        {
            body["tools"] = request.Tools.Select(ToFunctionTool).ToArray();
        }

        return body;
    }

    public static IReadOnlyList<ToolCall> ParseOllama(IReadOnlyList<OllamaToolCall>? calls)
    {
        if (calls is null || calls.Count == 0)
        {
            return [];
        }

        var parsed = new List<ToolCall>(calls.Count);
        foreach (var call in calls)
        {
            var name = call.Function?.Name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var id = string.IsNullOrWhiteSpace(call.Id)
                ? Guid.NewGuid().ToString("N")
                : call.Id.Trim();
            parsed.Add(new ToolCall(id, name, ArgumentsFromElement(call.Function?.Arguments)));
        }

        return parsed;
    }

    public static IReadOnlyList<ToolCall> ParseOpenAi(IReadOnlyList<OpenAiProviderToolCall>? calls)
    {
        if (calls is null || calls.Count == 0)
        {
            return [];
        }

        var parsed = new List<ToolCall>(calls.Count);
        foreach (var call in calls)
        {
            var name = call.Function?.Name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var id = string.IsNullOrWhiteSpace(call.Id)
                ? Guid.NewGuid().ToString("N")
                : call.Id.Trim();
            var arguments = string.IsNullOrWhiteSpace(call.Function?.Arguments)
                ? "{}"
                : call.Function!.Arguments!;
            parsed.Add(new ToolCall(id, name, arguments));
        }

        return parsed;
    }

    private static Dictionary<string, object?> ToOpenAiMessage(ChatCompletionMessage message)
    {
        var payload = new Dictionary<string, object?>
        {
            ["role"] = message.Role,
            ["content"] = message.Content
        };
        if (!string.IsNullOrWhiteSpace(message.ToolCallId))
        {
            payload["tool_call_id"] = message.ToolCallId;
        }

        if (!string.IsNullOrWhiteSpace(message.Name))
        {
            payload["name"] = message.Name;
        }

        if (message.ToolCalls is { Count: > 0 })
        {
            payload["tool_calls"] = message.ToolCalls.Select(call => new Dictionary<string, object?>
            {
                ["id"] = call.Id,
                ["type"] = "function",
                ["function"] = new Dictionary<string, object?>
                {
                    ["name"] = call.Name,
                    ["arguments"] = call.ArgumentsJson
                }
            }).ToArray();
        }

        return payload;
    }

    private static Dictionary<string, object?> ToOllamaMessage(ChatCompletionMessage message)
    {
        var payload = new Dictionary<string, object?>
        {
            ["role"] = message.Role,
            ["content"] = message.Content
        };
        if (!string.IsNullOrWhiteSpace(message.Name))
        {
            payload["tool_name"] = message.Name;
        }

        if (message.ToolCalls is { Count: > 0 })
        {
            payload["tool_calls"] = message.ToolCalls.Select(call => new Dictionary<string, object?>
            {
                ["function"] = new Dictionary<string, object?>
                {
                    ["name"] = call.Name,
                    ["arguments"] = ParseJsonObject(call.ArgumentsJson)
                }
            }).ToArray();
        }

        return payload;
    }

    private static object ToFunctionTool(ToolDefinition definition)
    {
        return new Dictionary<string, object?>
        {
            ["type"] = "function",
            ["function"] = new Dictionary<string, object?>
            {
                ["name"] = definition.Name,
                ["description"] = definition.Description,
                ["parameters"] = new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["properties"] = definition.Parameters.ToDictionary(
                        parameter => parameter.Name,
                        parameter => new Dictionary<string, object?>
                        {
                            ["type"] = parameter.Type,
                            ["description"] = parameter.Description
                        }),
                    ["required"] = definition.Parameters
                        .Where(parameter => parameter.Required)
                        .Select(parameter => parameter.Name)
                        .ToArray()
                }
            }
        };
    }

    private static string ArgumentsFromElement(JsonElement? arguments)
    {
        if (arguments is null)
        {
            return "{}";
        }

        return arguments.Value.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => "{}",
            JsonValueKind.String => string.IsNullOrWhiteSpace(arguments.Value.GetString())
                ? "{}"
                : arguments.Value.GetString()!,
            JsonValueKind.Object => arguments.Value.GetRawText(),
            _ => "{}"
        };
    }

    private static object ParseJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JsonSerializer.Deserialize<JsonElement>("{}");
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (JsonException)
        {
            return json;
        }
    }
}

internal sealed record OpenAiProviderToolCall(
    string? Id,
    OpenAiProviderToolFunction? Function);

internal sealed record OpenAiProviderToolFunction(
    string? Name,
    string? Arguments);
