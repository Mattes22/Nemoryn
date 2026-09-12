namespace Memory.Application.Tools;

using Memory.Domain.Tools;

internal sealed class ToolRuntime(IToolRegistry registry, IToolAuditService toolAudit) : IToolRuntime
{
    public const int MaxArgumentChars = 4000;
    public static readonly TimeSpan InvokeTimeout = TimeSpan.FromSeconds(8);

    public async Task<ToolResult> InvokeAsync(
        ToolCall call,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(context);

        var name = call.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return await CompleteAsync(
                call,
                context,
                definition: null,
                ToolAuditOutcome.Invalid,
                new ToolResult(call.Id, name, false, "Tool name is required."),
                cancellationToken);
        }

        var argumentsJson = call.ArgumentsJson ?? string.Empty;
        if (argumentsJson.Length > MaxArgumentChars)
        {
            return await CompleteAsync(
                call with { Name = name },
                context,
                definition: null,
                ToolAuditOutcome.Invalid,
                new ToolResult(call.Id, name, false, "Tool arguments exceeded the size limit."),
                cancellationToken);
        }

        var tool = registry.Get(name);
        if (tool is null)
        {
            return await CompleteAsync(
                call with { Name = name, ArgumentsJson = argumentsJson },
                context,
                definition: null,
                ToolAuditOutcome.Unknown,
                new ToolResult(call.Id, name, false, $"Unknown tool '{name}'."),
                cancellationToken);
        }

        if (!ToolAuthorization.TryAuthorize(tool.Definition, context, out var reason))
        {
            return await CompleteAsync(
                call with { Name = name, ArgumentsJson = argumentsJson },
                context,
                tool.Definition,
                ToolAuditOutcome.Denied,
                new ToolResult(call.Id, name, false, reason),
                cancellationToken);
        }

        if (tool.Definition.Capabilities.Contains(ToolCapability.Network)
            && context.Permissions is not null
            && !context.Permissions.TryConsumeNetwork())
        {
            return await CompleteAsync(
                call with { Name = name, ArgumentsJson = argumentsJson },
                context,
                tool.Definition,
                ToolAuditOutcome.Denied,
                new ToolResult(
                    call.Id,
                    name,
                    false,
                    $"Tool '{name}' is not allowed: NetworkOnce already used."),
                cancellationToken);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(InvokeTimeout);
        try
        {
            var result = await tool.InvokeAsync(
                call with { Name = name, ArgumentsJson = argumentsJson },
                context,
                timeout.Token);
            return await CompleteAsync(
                call with { Name = name, ArgumentsJson = argumentsJson },
                context,
                tool.Definition,
                result.Ok ? ToolAuditOutcome.Succeeded : ToolAuditOutcome.Failed,
                result,
                cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await CompleteAsync(
                call with { Name = name, ArgumentsJson = argumentsJson },
                context,
                tool.Definition,
                ToolAuditOutcome.TimedOut,
                new ToolResult(call.Id, name, false, $"Tool '{name}' timed out."),
                cancellationToken);
        }
        catch (Exception exception)
        {
            return await CompleteAsync(
                call with { Name = name, ArgumentsJson = argumentsJson },
                context,
                tool.Definition,
                ToolAuditOutcome.Failed,
                new ToolResult(call.Id, name, false, $"Tool '{name}' failed: {exception.Message}"),
                cancellationToken);
        }
    }

    private async Task<ToolResult> CompleteAsync(
        ToolCall call,
        ToolContext context,
        ToolDefinition? definition,
        ToolAuditOutcome outcome,
        ToolResult result,
        CancellationToken cancellationToken)
    {
        await toolAudit.RecordAsync(context, call, definition, outcome, result, cancellationToken);
        return result;
    }
}
