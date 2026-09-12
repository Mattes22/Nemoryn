namespace Memory.Application.Runtime;

public sealed record RuntimeMemoryStatus(
    string Status,
    string Summary,
    bool DatabaseReachable,
    string? LatestMigration,
    int PendingCandidates,
    int PendingConflicts);

public sealed record RuntimeModelStatus(
    string Status,
    string Summary,
    string Provider,
    string BaseUrl,
    string ChatModel,
    string EmbeddingModel,
    bool ChatAvailable,
    bool EmbeddingAvailable,
    bool ExtractorAvailable,
    bool ProviderReachable,
    int? ProviderLatencyMs,
    string? ProviderError);

public sealed record RuntimeWorkerDetail(
    string Name,
    string Status,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? LastSuccessAt,
    bool? LastSucceeded,
    string? LastError);

public sealed record RuntimeWorkerStatus(
    string Status,
    string Summary,
    RuntimeWorkerDetail Ingestion,
    RuntimeWorkerDetail Retention,
    int PendingIngestionJobs,
    int ProcessingIngestionJobs);

public sealed record RuntimeStatusResponse(
    DateTimeOffset CheckedAt,
    string Overall,
    RuntimeMemoryStatus Memory,
    RuntimeModelStatus Model,
    RuntimeWorkerStatus Workers);
