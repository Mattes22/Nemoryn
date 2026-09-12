namespace Memory.Application.Memories;

public sealed record MemoryRetentionResult(
    int ArchivedMemories,
    int DiscardedCandidates,
    int DeletedJobs);
