namespace Memory.Application.Memories;

public sealed record MemoryRecallCase(
    string? Id,
    string Query,
    IReadOnlyList<Guid>? ExpectedMemoryIds,
    IReadOnlyList<string>? ExpectedContentContains);
