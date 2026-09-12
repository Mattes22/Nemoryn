namespace Memory.Application.Memories;

public sealed record MemoryCleanupApplyResponse(
    int RequestedCount,
    int ArchivedCount,
    IReadOnlyList<MemoryResponse> Archived);
