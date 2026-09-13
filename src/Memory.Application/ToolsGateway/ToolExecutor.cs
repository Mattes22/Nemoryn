namespace Memory.Application.ToolsGateway;

using System.Diagnostics;
using System.Text.Json;

internal sealed class ToolExecutor(
    IToolRegistry registry,
    ICapabilityAuthorizer authorizer,
    IToolInvocationAuditor auditor,
    TimeProvider timeProvider) : IToolExecutor
{
    public async Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        JsonElement arguments,
        ToolCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var name = toolName?.Trim() ?? string.Empty;
        var started = Stopwatch.GetTimestamp();
        try
        {
            if (name.Length == 0)
            {
                return Complete(
                    caller,
                    name,
                    ToolExecutionStatus.NotFound,
                    allowed: false,
                    ok: false,
                    result: null,
                    "Tool name is required.",
                    started);
            }

            var tool = registry.Get(name);
            if (tool is null)
            {
                return Complete(
                    caller,
                    name,
                    ToolExecutionStatus.NotFound,
                    allowed: false,
                    ok: false,
                    result: null,
                    $"Unknown tool '{name}'.",
                    started);
            }

            if (!authorizer.IsAuthorized(tool, caller.Capabilities))
            {
                return Complete(
                    caller,
                    name,
                    ToolExecutionStatus.Denied,
                    allowed: false,
                    ok: false,
                    result: null,
                    $"Tool '{name}' is not allowed.",
                    started);
            }

            var payload = await tool.ExecuteAsync(arguments, cancellationToken);
            return Complete(
                caller,
                name,
                ToolExecutionStatus.Succeeded,
                allowed: true,
                ok: true,
                payload,
                error: null,
                started);
        }
        catch (UnsafeUrlException exception)
        {
            return Complete(
                caller,
                name,
                ToolExecutionStatus.ForbiddenUrl,
                allowed: true,
                ok: false,
                result: null,
                exception.Message,
                started);
        }
        catch (ArgumentException exception)
        {
            return Complete(
                caller,
                name,
                ToolExecutionStatus.InvalidArguments,
                allowed: true,
                ok: false,
                result: null,
                exception.Message,
                started);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Complete(
                caller,
                name,
                ToolExecutionStatus.TimedOut,
                allowed: true,
                ok: false,
                result: null,
                $"Tool '{name}' timed out.",
                started);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Complete(
                caller,
                name,
                ToolExecutionStatus.ProviderError,
                allowed: true,
                ok: false,
                result: null,
                $"Tool '{name}' failed: {exception.Message}",
                started);
        }
    }

    private ToolExecutionResult Complete(
        ToolCaller caller,
        string toolName,
        ToolExecutionStatus status,
        bool allowed,
        bool ok,
        object? result,
        string? error,
        long startedTimestamp)
    {
        var durationMs = (int)Math.Max(
            0,
            Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds);
        var execution = new ToolExecutionResult(
            toolName,
            status,
            allowed,
            ok,
            result,
            error,
            durationMs);

        auditor.Record(new ToolInvocationAudit(
            caller.Id,
            string.IsNullOrEmpty(toolName) ? "(missing)" : toolName,
            timeProvider.GetUtcNow(),
            allowed,
            ok,
            status,
            durationMs,
            error));

        return execution;
    }
}
