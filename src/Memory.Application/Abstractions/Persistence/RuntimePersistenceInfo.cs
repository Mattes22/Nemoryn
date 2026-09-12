namespace Memory.Application.Abstractions.Persistence;

public sealed record DatabaseRuntimeInfo(
    bool CanConnect,
    string? LatestMigration);

public sealed record RuntimeQueueCounts(
    int PendingIngestionJobs,
    int ProcessingIngestionJobs,
    int PendingCandidates,
    int PendingConflicts);
