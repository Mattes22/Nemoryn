namespace Memory.Application.Tools;

using System.Text;
using System.Text.Json;

internal sealed class HttpTool : ITool
{
    public const string HttpClientName = "MemoryExternalTools";
    public const int MaxResponseChars = 8192;
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private static readonly HashSet<string> BuiltinNames = new(StringComparer.Ordinal)
    {
        "get_time",
        "search_memories"
    };

    private readonly HttpClient _httpClient;
    private readonly Uri _url;
    private readonly HttpMethod _method;

    private HttpTool(ToolDefinition definition, HttpClient httpClient, Uri url, HttpMethod method)
    {
        Definition = definition;
        _httpClient = httpClient;
        _url = url;
        _method = method;
    }

    public ToolDefinition Definition { get; }

    public static HttpTool Create(ExternalToolDefinition definition, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(httpClient);
        return new HttpTool(
            BuildDefinition(definition, out var url, out var method),
            httpClient,
            url,
            method);
    }

    public async Task<ToolResult> InvokeAsync(
        ToolCall call,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(context);

        var payload = NormalizeArguments(call.ArgumentsJson);
        using var request = new HttpRequestMessage(_method, _url);
        request.Headers.TryAddWithoutValidation("User-Agent", "Memory.HttpTool/1.0");
        if (_method == HttpMethod.Post)
        {
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var truncated = Truncate(body);
            if (!response.IsSuccessStatusCode)
            {
                return new ToolResult(
                    call.Id,
                    Definition.Name,
                    false,
                    $"HTTP {(int)response.StatusCode}: {truncated}");
            }

            return new ToolResult(call.Id, Definition.Name, true, truncated);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ToolResult(call.Id, Definition.Name, false, $"Tool '{Definition.Name}' timed out.");
        }
        catch (HttpRequestException exception)
        {
            return new ToolResult(
                call.Id,
                Definition.Name,
                false,
                $"Tool '{Definition.Name}' request failed: {exception.Message}");
        }
    }

    internal static ToolDefinition BuildDefinition(
        ExternalToolDefinition definition,
        out Uri url,
        out HttpMethod method)
    {
        var name = definition.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            throw new InvalidOperationException("External tool name is required.");
        }

        if (BuiltinNames.Contains(name))
        {
            throw new InvalidOperationException($"External tool name '{name}' collides with a built-in tool.");
        }

        var description = string.IsNullOrWhiteSpace(definition.Description)
            ? $"External HTTP tool '{name}'."
            : definition.Description.Trim();

        if (!Uri.TryCreate(definition.Url?.Trim(), UriKind.Absolute, out url!)
            || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(url.Host)
            || !string.IsNullOrEmpty(url.UserInfo))
        {
            throw new InvalidOperationException($"External tool '{name}' must use an absolute http(s) URL.");
        }

        method = ParseMethod(definition.Method, name);
        var parameters = (definition.Parameters ?? [])
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
            .Select(parameter => new ToolParameter(
                parameter.Name.Trim(),
                string.IsNullOrWhiteSpace(parameter.Type) ? "string" : parameter.Type.Trim(),
                parameter.Description?.Trim() ?? string.Empty,
                parameter.Required))
            .ToArray();

        return new ToolDefinition(
            name,
            description,
            parameters,
            ToolTrust.Untrusted,
            ParseCapabilities(definition.Capabilities));
    }

    private static IReadOnlyList<ToolCapability> ParseCapabilities(IEnumerable<string>? capabilities)
    {
        var parsed = new HashSet<ToolCapability>();
        foreach (var raw in capabilities ?? [])
        {
            if (!Enum.TryParse<ToolCapability>(raw.Trim(), ignoreCase: true, out var capability))
            {
                continue;
            }

            if (capability == ToolCapability.MemoryRead)
            {
                continue;
            }

            parsed.Add(capability);
        }

        parsed.Add(ToolCapability.Network);
        return parsed.OrderBy(capability => (int)capability).ToArray();
    }

    private static HttpMethod ParseMethod(string? method, string name)
    {
        var normalized = string.IsNullOrWhiteSpace(method) ? "POST" : method.Trim().ToUpperInvariant();
        return normalized switch
        {
            "POST" => HttpMethod.Post,
            "GET" => HttpMethod.Get,
            _ => throw new InvalidOperationException($"External tool '{name}' method must be GET or POST.")
        };
    }

    private static string NormalizeArguments(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.GetRawText()
                : "{}";
        }
        catch (JsonException)
        {
            return "{}";
        }
    }

    private static string Truncate(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.Length <= MaxResponseChars ? text : text[..MaxResponseChars];
    }
}
