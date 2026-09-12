namespace Memory.Application.Memories;

public sealed record MemoryCleanupPreviewResponse(
    string OwnerId,
    int ScannedCount,
    IReadOnlyList<MemoryCleanupSuggestion> Suggestions);
