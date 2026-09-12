namespace Memory.Application.Agent;

public sealed record AgentToolTrace(
    string CallId,
    string Name,
    string ArgumentsJson,
    bool Ok,
    string Result);
