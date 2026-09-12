namespace Memory.Domain.Memories;

public enum MemoryAuditAction
{
    Created = 1,
    Pinned = 2,
    Unpinned = 3,
    Edited = 4,
    Forgotten = 5,
    Superseded = 6,
    Promoted = 7,
    Rejected = 8,
    ConflictOpened = 9,
    ConflictAccepted = 10,
    ConflictKept = 11
}
