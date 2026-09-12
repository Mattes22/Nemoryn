namespace Memory.Domain.Memories;

public static class MemoryScopeRules
{
    public static MemoryScope DefaultFor(MemoryType type)
    {
        return type == MemoryType.Summary ? MemoryScope.Conversation : MemoryScope.User;
    }
}
