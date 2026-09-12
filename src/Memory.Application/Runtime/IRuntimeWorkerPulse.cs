namespace Memory.Application.Runtime;

public interface IRuntimeWorkerPulse
{
    RuntimeWorkerPulseSnapshot Ingestion { get; }
    RuntimeWorkerPulseSnapshot Retention { get; }

    void RecordIngestion(bool processed, string? error = null, DateTimeOffset? at = null);
    void RecordRetention(bool processed, string? error = null, DateTimeOffset? at = null);
    void HeartbeatIngestion(DateTimeOffset? at = null);
    void HeartbeatRetention(DateTimeOffset? at = null);
}

public sealed record RuntimeWorkerPulseSnapshot(
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? LastSuccessAt,
    bool? LastSucceeded,
    string? LastError);
