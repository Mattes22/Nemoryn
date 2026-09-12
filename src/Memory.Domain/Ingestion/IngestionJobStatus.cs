namespace Memory.Domain.Ingestion;

public enum IngestionJobStatus
{
    Pending = 1,
    Processing = 2,
    Succeeded = 3,
    Failed = 4,
    Skipped = 5
}
