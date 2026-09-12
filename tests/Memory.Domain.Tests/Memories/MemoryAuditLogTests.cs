namespace Memory.Domain.Tests.Memories;

using Memory.Domain.Memories;

public sealed class MemoryAuditLogTests
{
    [Fact]
    public void Constructor_requires_owner_and_known_action()
    {
        Assert.Throws<ArgumentException>(() =>
            new MemoryAuditLog(" ", MemoryAuditAction.Created, MemoryAuditActorKind.User));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MemoryAuditLog("user-1", (MemoryAuditAction)0, MemoryAuditActorKind.User));
    }

    [Fact]
    public void User_actor_id_is_the_owner()
    {
        var memory = new Memory(
            "user-1",
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            MemoryScope.User,
            "User likes tea.",
            MemoryType.Preference,
            0.9m,
            0.8m);

        var entry = MemoryAuditLog.ForMemory(
            MemoryAuditAction.Pinned,
            memory,
            MemoryAuditActorKind.User,
            "Memory pinned.");

        Assert.Equal("user-1", entry.ActorId);
        Assert.Equal(MemoryAuditActorKind.User, entry.ActorKind);
        Assert.Equal(memory.Id, entry.MemoryId);
        Assert.Equal("Memory pinned.", entry.Reason);
    }

    [Fact]
    public void Ingestion_actor_id_is_stable()
    {
        var memory = new Memory(
            "user-1",
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            MemoryScope.User,
            "User likes tea.",
            MemoryType.Preference,
            0.9m,
            0.8m);

        var entry = MemoryAuditLog.ForMemory(
            MemoryAuditAction.Created,
            memory,
            MemoryAuditActorKind.Ingestion,
            "Inferred memory auto-promoted.");

        Assert.Equal("ingestion", entry.ActorId);
        Assert.Equal(MemoryAuditActorKind.Ingestion, entry.ActorKind);
    }

    [Fact]
    public void Retention_actor_id_is_stable()
    {
        var memory = new Memory(
            "user-1",
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            MemoryScope.User,
            "User likes tea.",
            MemoryType.Preference,
            0.9m,
            0.8m);

        var entry = MemoryAuditLog.ForMemory(
            MemoryAuditAction.Forgotten,
            memory,
            MemoryAuditActorKind.Retention,
            "Memory expired and archived by retention.");

        Assert.Equal("retention", entry.ActorId);
        Assert.Equal(MemoryAuditActorKind.Retention, entry.ActorKind);
        Assert.Equal(MemoryAuditAction.Forgotten, entry.Action);
    }
}
