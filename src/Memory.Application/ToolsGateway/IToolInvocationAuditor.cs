namespace Memory.Application.ToolsGateway;

public interface IToolInvocationAuditor
{
    void Record(ToolInvocationAudit audit);
}

public sealed record ToolInvocationAudit(
    string CallerId,
    string ToolName,
    DateTimeOffset OccurredAt,
    bool Allowed,
    bool Ok,
    ToolExecutionStatus Status,
    int DurationMs,
    string? Error);
