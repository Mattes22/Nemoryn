namespace Memory.Application.Tools;

using Memory.Domain.Tools;

public sealed record ToolAuditLogResponse(
    Guid Id,
    string OwnerId,
    Guid? ConversationId,
    string CallId,
    string Name,
    string? ArgumentsJson,
    string? Trust,
    IReadOnlyList<string> Capabilities,
    bool Invoked,
    bool Ok,
    ToolAuditOutcome Outcome,
    string? Result,
    string? Error,
    DateTimeOffset OccurredAt);
