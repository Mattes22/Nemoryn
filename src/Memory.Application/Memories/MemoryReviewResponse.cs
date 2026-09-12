namespace Memory.Application.Memories;

public sealed record MemoryReviewResponse(
    string OwnerId,
    Guid? ConversationId,
    int AttentionCount,
    IReadOnlyList<MemoryResponse> ActiveMemories,
    IReadOnlyList<MemoryCandidateResponse> PendingCandidates,
    IReadOnlyList<MemoryConflictResponse> PendingConflicts,
    MemoryCleanupPreviewResponse Cleanup);
