namespace Memory.Application.Tools;

using Memory.Domain.Tools;

public interface IToolAuditService
{
    Task RecordAsync(
        ToolContext context,
        ToolCall call,
        ToolDefinition? definition,
        ToolAuditOutcome outcome,
        ToolResult result,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ToolAuditLogResponse>> GetForOwnerAsync(
        string ownerId,
        Guid? conversationId = null,
        int take = 50,
        CancellationToken cancellationToken = default);
}
