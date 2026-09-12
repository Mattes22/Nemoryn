namespace Memory.Application.Memories;

public sealed record MemoryCleanupApplyRequest(IReadOnlyList<Guid> MemoryIds);
