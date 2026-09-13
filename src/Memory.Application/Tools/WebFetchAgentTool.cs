namespace Memory.Application.Tools;

using System.Net.Http;
using System.Text.Json;
using Memory.Application.ToolsGateway;
using Memory.Application.ToolsGateway.Web;

internal sealed class WebFetchAgentTool(IWebContentFetcher contentFetcher) : ITool
{
    public const string ToolName = "web_fetch";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly WebFetchTool _inner = new(contentFetcher);

    public ToolDefinition Definition { get; } = new(
        ToolName,
        "Fetch one public http(s) page and return cleaned text. Use after web_search when a hit looks relevant. Private, loopback, and local addresses are blocked.",
        [
            new ToolParameter(
                "url",
                "string",
                "Absolute http or https URL of a public page.",
                Required: true)
        ],
        ToolTrust.Builtin,
        [ToolCapability.WebRead]);

    public async Task<ToolResult> InvokeAsync(
        ToolCall call,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(context);

        if (!TryReadUrl(call, out var url, out var error))
        {
            return Failed(call, error);
        }

        try
        {
            var result = await _inner.FetchAsync(new WebFetchArguments(url), cancellationToken);
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
            exception is ArgumentException or UnsafeUrlException or InvalidOperationException or HttpRequestException)
        {
            return Failed(call, exception.Message);
        }
    }

    private static bool TryReadUrl(ToolCall call, out string url, out string error)
    {
        url = string.Empty;
        error = string.Empty;
        var argumentsJson = string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson.Trim();
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "Arguments must be a JSON object.";
                return false;
            }

            if (!document.RootElement.TryGetProperty("url", out var urlElement)
                || urlElement.ValueKind == JsonValueKind.Null
                || urlElement.ValueKind == JsonValueKind.Undefined)
            {
                error = "url is required.";
                return false;
            }

            if (urlElement.ValueKind != JsonValueKind.String)
            {
                error = "url must be a string.";
                return false;
            }

            url = urlElement.GetString()?.Trim() ?? string.Empty;
        }
        catch (JsonException)
        {
            error = "Invalid JSON arguments.";
            return false;
        }

        if (url.Length == 0)
        {
            error = "url is required.";
            return false;
        }

        return true;
    }

    private ToolResult Failed(ToolCall call, string message)
        => new(call.Id, Definition.Name, false, message);
}
