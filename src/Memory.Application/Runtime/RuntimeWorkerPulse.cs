namespace Memory.Application.Runtime;

internal sealed class RuntimeWorkerPulse : IRuntimeWorkerPulse
{
    private readonly object _gate = new();
    private RuntimeWorkerPulseSnapshot _ingestion = new(null, null, null, null);
    private RuntimeWorkerPulseSnapshot _retention = new(null, null, null, null);

    public RuntimeWorkerPulseSnapshot Ingestion
    {
        get
        {
            lock (_gate)
            {
                return _ingestion;
            }
        }
    }

    public RuntimeWorkerPulseSnapshot Retention
    {
        get
        {
            lock (_gate)
            {
                return _retention;
            }
        }
    }

    public void RecordIngestion(bool processed, string? error = null, DateTimeOffset? at = null)
    {
        lock (_gate)
        {
            _ingestion = Next(_ingestion, processed, error, at);
        }
    }

    public void RecordRetention(bool processed, string? error = null, DateTimeOffset? at = null)
    {
        lock (_gate)
        {
            _retention = Next(_retention, processed, error, at);
        }
    }

    public void HeartbeatIngestion(DateTimeOffset? at = null)
    {
        lock (_gate)
        {
            _ingestion = Heartbeat(_ingestion, at);
        }
    }

    public void HeartbeatRetention(DateTimeOffset? at = null)
    {
        lock (_gate)
        {
            _retention = Heartbeat(_retention, at);
        }
    }

    private static RuntimeWorkerPulseSnapshot Next(
        RuntimeWorkerPulseSnapshot current,
        bool processed,
        string? error,
        DateTimeOffset? at)
    {
        var when = at ?? DateTimeOffset.UtcNow;
        var succeeded = error is null;
        return new RuntimeWorkerPulseSnapshot(
            when,
            succeeded ? when : current.LastSuccessAt,
            succeeded,
            string.IsNullOrWhiteSpace(error) ? null : error.Trim());
    }

    private static RuntimeWorkerPulseSnapshot Heartbeat(
        RuntimeWorkerPulseSnapshot current,
        DateTimeOffset? at)
    {
        return current with { LastAttemptAt = at ?? DateTimeOffset.UtcNow };
    }
}
