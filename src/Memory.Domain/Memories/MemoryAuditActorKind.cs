namespace Memory.Domain.Memories;

public enum MemoryAuditActorKind
{
    User = 1,
    Ingestion = 2,
    Cleanup = 3,
    Retention = 4
}
