namespace Memory.Application.Tools;

using System.Net.Http;
using System.Text.Json;
using Memory.Application.ToolsGateway.Web;

internal sealed class WebSearchAgentTool(IWebSearchProvider searchProvider) : ITool
{
    public const string ToolName = "web_search";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly WebSearchTool _inner = new(searchProvider);

    public ToolDefinition Definition { get; } = new(
        ToolName,
        "Search the public internet. Use this when the user asks to find, look up, or get information about a person, company, news, or any topic that is not already in memory. Do not claim you searched unless you called this tool.",
        [
            new ToolParameter("query", "string", "Search query.", Required: true),
            new ToolParameter(
                "maxResults",
                "integer",
                "Maximum hits to return. Defaults to 5, capped at 10.",
                Required: false)
        ],
        ToolTrust.Builtin,
        [ToolCapability.WebSearch]);

    public async Task<ToolResult> InvokeAsync(
        ToolCall call,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(context);

        if (!TryReadArguments(call, out var arguments, out var error))
        {
            return Failed(call, error);
        }

        try
        {
            var result = await _inner.SearchAsync(arguments, cancellationToken);
            return new ToolResult(
                call.Id,
                Definition.Name,
                true,
                JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            return Failed(call, exception.Message);
        }
    }

    private static bool TryReadArguments(
        ToolCall call,
        out WebSearchArguments arguments,
        out string error)
    {
        arguments = new(string.Empty);
        error = string.Empty;
        string? query = null;
        int? maxResults = null;
        var argumentsJson = string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson.Trim();
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "Arguments must be a JSON object.";
                return false;
            }

            if (document.RootElement.TryGetProperty("query", out var queryElement)
                && queryElement.ValueKind != JsonValueKind.Null
                && queryElement.ValueKind != JsonValueKind.Undefined)
            {
                if (queryElement.ValueKind != JsonValueKind.String)
                {
                    error = "query must be a string.";
                    return false;
                }

                query = queryElement.GetString();
            }

            if (document.RootElement.TryGetProperty("maxResults", out var maxElement)
                && maxElement.ValueKind != JsonValueKind.Null
                && maxElement.ValueKind != JsonValueKind.Undefined)
            {
                if (!TryReadInt(maxElement, out maxResults))
                {
                    error = "maxResults must be a number.";
                    return false;
                }
            }
        }
        catch (JsonException)
        {
            error = "Invalid JSON arguments.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            error = "query is required.";
            return false;
        }

        arguments = new(query.Trim(), maxResults);
        return true;
    }

    private static bool TryReadInt(JsonElement element, out int? value)
    {
        value = null;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var integer))
        {
            value = integer;
            return true;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var number))
        {
            value = (int)Math.Truncate(number);
            return true;
        }

        return false;
    }

    private ToolResult Failed(ToolCall call, string message)
        => new(call.Id, Definition.Name, false, message);
}
