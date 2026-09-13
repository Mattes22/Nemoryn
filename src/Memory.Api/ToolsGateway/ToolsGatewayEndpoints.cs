namespace Memory.Api.ToolsGateway;

using System.Text.Json;
using Microsoft.Extensions.Options;
using Memory.Application.Configuration;
using Memory.Application.ToolsGateway;
using Memory.Application.ToolsGateway.Web;

internal static class ToolsGatewayEndpoints
{
    public const string CallerHeader = "X-Nemoryn-Caller";
    public const string CapabilitiesHeader = "X-Nemoryn-Capabilities";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static void MapToolsGatewayEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/tools")
            .WithTags("Tools Gateway")
            .WithGroupName("tools");

        group.MapGet(string.Empty, ListTools)
            .WithName("ListToolsGateway")
            .WithSummary("List tools available for the caller's granted capabilities.");

        group.MapPost("/web.search/execute", ExecuteWebSearch)
            .WithName("ExecuteWebSearch")
            .WithSummary("Execute web.search against the configured search provider.");

        group.MapPost("/web.fetch/execute", ExecuteWebFetch)
            .WithName("ExecuteWebFetch")
            .WithSummary("Fetch a public http(s) page and return cleaned text.");

        group.MapPost("/{toolName}/execute", ExecuteTool)
            .WithName("ExecuteTool")
            .WithSummary("Execute a registered tool by name. Arguments must match the tool contract.");
    }

    private static IResult ListTools(
        HttpContext httpContext,
        IToolRegistry registry,
        IOptions<ToolsOptions> toolsOptions)
    {
        var caller = ResolveCaller(httpContext, toolsOptions.Value);
        return Results.Ok(new ToolCatalogResponse(registry.DescribeAvailable(caller.Capabilities)));
    }

    private static Task<IResult> ExecuteWebSearch(
        WebSearchArguments request,
        HttpContext httpContext,
        IToolExecutor executor,
        IOptions<ToolsOptions> toolsOptions,
        CancellationToken cancellationToken)
    {
        var arguments = JsonSerializer.SerializeToElement(request, JsonOptions);
        return ExecuteCore(GatewayToolNames.WebSearch, arguments, httpContext, executor, toolsOptions.Value, cancellationToken);
    }

    private static Task<IResult> ExecuteWebFetch(
        WebFetchArguments request,
        HttpContext httpContext,
        IToolExecutor executor,
        IOptions<ToolsOptions> toolsOptions,
        CancellationToken cancellationToken)
    {
        var arguments = JsonSerializer.SerializeToElement(request, JsonOptions);
        return ExecuteCore(GatewayToolNames.WebFetch, arguments, httpContext, executor, toolsOptions.Value, cancellationToken);
    }

    private static Task<IResult> ExecuteTool(
        string toolName,
        JsonElement arguments,
        HttpContext httpContext,
        IToolExecutor executor,
        IOptions<ToolsOptions> toolsOptions,
        CancellationToken cancellationToken)
    {
        return ExecuteCore(toolName, arguments, httpContext, executor, toolsOptions.Value, cancellationToken);
    }

    private static async Task<IResult> ExecuteCore(
        string toolName,
        JsonElement arguments,
        HttpContext httpContext,
        IToolExecutor executor,
        ToolsOptions options,
        CancellationToken cancellationToken)
    {
        var caller = ResolveCaller(httpContext, options);
        var execution = await executor.ExecuteAsync(toolName, arguments, caller, cancellationToken);
        var body = new ToolExecuteHttpResponse(
            execution.ToolName,
            execution.Status.ToString(),
            execution.Ok,
            execution.Result,
            execution.Error,
            execution.DurationMs);

        var statusCode = execution.Status switch
        {
            ToolExecutionStatus.Succeeded => StatusCodes.Status200OK,
            ToolExecutionStatus.Denied => StatusCodes.Status403Forbidden,
            ToolExecutionStatus.NotFound => StatusCodes.Status404NotFound,
            ToolExecutionStatus.InvalidArguments => StatusCodes.Status400BadRequest,
            ToolExecutionStatus.ForbiddenUrl => StatusCodes.Status400BadRequest,
            ToolExecutionStatus.TimedOut => StatusCodes.Status504GatewayTimeout,
            _ => StatusCodes.Status502BadGateway
        };

        return Results.Json(body, JsonOptions, statusCode: statusCode);
    }

    internal static ToolCaller ResolveCaller(HttpContext httpContext, ToolsOptions options)
    {
        var callerId = Header(httpContext, CallerHeader)
            ?? Header(httpContext, "X-User-Id")
            ?? "anonymous";
        var requested = ParseCapabilities(Header(httpContext, CapabilitiesHeader))
            ?? ParseCapabilities(httpContext.Request.Query["capabilities"].FirstOrDefault());
        var server = ToolCapabilities.NormalizeAll(options.DefaultCapabilities);
        IReadOnlySet<string> granted = requested is null
            ? server
            : server.Intersect(requested, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

        return new ToolCaller(callerId, granted);
    }

    private static HashSet<string>? ParseCapabilities(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return ToolCapabilities.NormalizeAll(
            raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string? Header(HttpContext httpContext, string name)
    {
        if (!httpContext.Request.Headers.TryGetValue(name, out var values))
        {
            return null;
        }

        var value = values.ToString().Trim();
        return value.Length == 0 ? null : value;
    }
}

internal sealed record ToolCatalogResponse(IReadOnlyList<ToolDescriptor> Tools);

internal sealed record ToolExecuteHttpResponse(
    string Tool,
    string Status,
    bool Ok,
    object? Result,
    string? Error,
    int DurationMs);
