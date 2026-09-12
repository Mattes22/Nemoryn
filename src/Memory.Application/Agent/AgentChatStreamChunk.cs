namespace Memory.Application.Agent;

public sealed record AgentChatStreamChunk(string? Delta, AgentChatResponse? Completed);
