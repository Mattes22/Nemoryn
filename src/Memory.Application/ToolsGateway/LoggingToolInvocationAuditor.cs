namespace Memory.Application.ToolsGateway;

using Microsoft.Extensions.Logging;

internal sealed class LoggingToolInvocationAuditor(ILogger<LoggingToolInvocationAuditor> logger)
    : IToolInvocationAuditor
{
    public void Record(ToolInvocationAudit audit)
    {
        ArgumentNullException.ThrowIfNull(audit);

        logger.LogInformation(
            "Tools gateway caller={Caller} tool={Tool} allowed={Allowed} ok={Ok} status={Status} durationMs={DurationMs} error={Error}",
            audit.CallerId,
            audit.ToolName,
            audit.Allowed,
            audit.Ok,
            audit.Status,
            audit.DurationMs,
            audit.Error);
    }
}
