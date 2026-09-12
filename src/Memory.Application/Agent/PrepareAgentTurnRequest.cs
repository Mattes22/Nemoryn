namespace Memory.Application.Agent;

public sealed record PrepareAgentTurnRequest(
    string? UserMessage,
    int? Limit,
    int? RecentMessageCount);
