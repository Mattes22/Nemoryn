namespace Memory.Domain.Memories;

public enum MemoryConflictStatus
{
    Pending = 1,
    CandidateAccepted = 2,
    ExistingKept = 3
}
