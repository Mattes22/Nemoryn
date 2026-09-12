namespace Memory.Application.Tools;

using Memory.Application.Abstractions.Persistence;
using Memory.Domain.Tools;

internal sealed class ToolAuditService(IMemoryStore memoryStore, TimeProvider timeProvider) : IToolAuditService
{
    public async Task RecordAsync(
        ToolContext context,
        ToolCall call,
        ToolDefinition? definition,
        ToolAuditOutcome outcome,
        ToolResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(result);

        if (string.IsNullOrWhiteSpace(context.OwnerId))
        {
            return;
        }

        var entry = new ToolAuditLog(
            context.OwnerId,
            call.Id,
            result.Name,
            outcome,
            context.ConversationId,
            call.ArgumentsJson,
            definition?.Trust.ToString(),
            definition?.Capabilities.Select(capability => capability.ToString()).ToArray(),
            result.Ok ? result.Content : null,
            result.Ok ? null : result.Content,
            timeProvider.GetUtcNow());

        memoryStore.AddToolAuditLog(entry);
        await memoryStore.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ToolAuditLogResponse>> GetForOwnerAsync(
        string ownerId,
        Guid? conversationId = null,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(ownerId));
        }

        var entries = await memoryStore.GetToolAuditLogsAsync(
            ownerId.Trim(),
            conversationId,
            take,
            cancellationToken);

        return entries.Select(ToResponse).ToArray();
    }

    internal static ToolAuditLogResponse ToResponse(ToolAuditLog entry)
    {
        return new ToolAuditLogResponse(
            entry.Id,
            entry.OwnerId,
            entry.ConversationId,
            entry.CallId,
            entry.Name,
            entry.ArgumentsJson,
            entry.Trust,
            entry.CapabilityNames,
            entry.Invoked,
            entry.Ok,
            entry.Outcome,
            entry.Result,
            entry.Error,
            entry.OccurredAt);
    }
}
