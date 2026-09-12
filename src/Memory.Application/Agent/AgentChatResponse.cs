namespace Memory.Application.Agent;

using Memory.Application.Conversations;
using Memory.Application.Tools;

public sealed record AgentChatResponse(
    string Status,
    string Provider,
    string Model,
    MessageResponse UserMessage,
    MessageResponse? AssistantMessage,
    PrepareAgentTurnResponse PreparedTurn,
    string? ErrorMessage,
    IReadOnlyList<AgentToolTrace> ToolTrace,
    ToolPermissionProfile PermissionProfile = ToolPermissionProfile.Safe);
