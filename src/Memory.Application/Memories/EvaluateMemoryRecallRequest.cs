namespace Memory.Application.Memories;

public sealed record EvaluateMemoryRecallRequest(
    IReadOnlyList<MemoryRecallCase> Cases,
    int? Limit);
