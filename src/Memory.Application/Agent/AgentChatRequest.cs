namespace Memory.Application.Agent;

using Memory.Application.Tools;

public sealed record AgentChatRequest(
    string Message,
    int? MemoryLimit,
    int? RecentMessageCount,
    ToolPermissionProfile? PermissionProfile = null,
    string? ChatModel = null);
