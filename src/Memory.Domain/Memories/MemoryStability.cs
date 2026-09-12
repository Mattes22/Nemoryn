namespace Memory.Domain.Memories;

public static class MemoryStability
{
    public const decimal DefaultCoreImportanceThreshold = 0.75m;
    public const decimal DefaultMinPersistConfidence = 0.55m;
    public const decimal DefaultMinPersistImportance = 0.35m;
    public const decimal DefaultSupersedeConfidenceMargin = 0.05m;

    public static bool IsCore(Memory memory, decimal importanceThreshold)
    {
        if (memory.Status != MemoryStatus.Active)
        {
            return false;
        }

        if (memory.IsPinned)
        {
            return true;
        }

        if (memory.Scope != MemoryScope.User)
        {
            return false;
        }

        if (memory.Type == MemoryType.Constraint)
        {
            return true;
        }

        return memory.Type is MemoryType.Fact or MemoryType.Preference or MemoryType.Goal or MemoryType.Relationship
            && memory.Importance >= importanceThreshold;
    }
}
