namespace Memory.Domain.Tests.Memories;

using Memory.Domain.Memories;

public sealed class MemoryTests
{
    private static readonly Guid ConversationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void Constructor_rejects_scores_outside_range()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create("User likes tea.", importance: 1.2m, confidence: 0.5m));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create("User likes tea.", importance: 0.5m, confidence: -0.1m));
    }

    [Fact]
    public void Constructor_rejects_invalid_validity_range()
    {
        var from = DateTimeOffset.Parse("2026-01-02T00:00:00Z");
        var until = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        Assert.Throws<ArgumentException>(() =>
            Create("Temporary fact.", type: MemoryType.Fact, validFrom: from, validUntil: until));
    }

    [Fact]
    public void Constructor_computes_stable_fingerprint()
    {
        var first = Create("User likes tea.");
        var second = Create("  user   LIKES tea. ");

        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(64, first.Fingerprint.Length);
    }

    [Fact]
    public void IsEffective_hides_expired_and_inactive()
    {
        var now = DateTimeOffset.UtcNow;
        var active = Create("User likes tea.");
        var expired = Create(
            "Temporary.",
            type: MemoryType.Fact,
            validFrom: now.AddDays(-2),
            validUntil: now.AddDays(-1));
        var superseded = Create("Old tea fact.");
        superseded.Supersede(active.Id);

        Assert.True(active.IsEffective(now));
        Assert.False(expired.IsEffective(now));
        Assert.False(superseded.IsEffective(now));
    }

    [Fact]
    public void Supersede_marks_memory_and_stores_replacement()
    {
        var memory = Create("Old fact.", type: MemoryType.Fact);
        var replacementId = Guid.NewGuid();

        memory.Supersede(replacementId);

        Assert.Equal(MemoryStatus.Superseded, memory.Status);
        Assert.Equal(replacementId, memory.SupersededByMemoryId);
    }

    [Fact]
    public void Supersede_rejects_self_replacement_and_inactive()
    {
        var memory = Create("Old fact.", type: MemoryType.Fact);

        Assert.Throws<ArgumentException>(() => memory.Supersede(memory.Id));

        memory.Archive();
        Assert.Throws<InvalidOperationException>(() => memory.Supersede(Guid.NewGuid()));
    }

    [Fact]
    public void Archive_and_reject_change_status()
    {
        var archived = Create("Old fact.", type: MemoryType.Fact);
        var rejected = Create("Bad fact.", type: MemoryType.Fact);

        archived.Archive();
        rejected.Reject();

        Assert.Equal(MemoryStatus.Archived, archived.Status);
        Assert.Equal(MemoryStatus.Rejected, rejected.Status);
    }

    [Fact]
    public void SetEmbedding_copies_values()
    {
        var memory = Create("User likes tea.");
        var embedding = new[] { 0.1f, 0.2f, 0.3f };

        memory.SetEmbedding(embedding);
        embedding[0] = 9f;

        Assert.Equal(new[] { 0.1f, 0.2f, 0.3f }, memory.Embedding);
    }

    [Fact]
    public void Scope_defaults_summary_to_conversation()
    {
        Assert.Equal(MemoryScope.Conversation, MemoryScopeRules.DefaultFor(MemoryType.Summary));
        Assert.Equal(MemoryScope.User, MemoryScopeRules.DefaultFor(MemoryType.Preference));
    }

    [Fact]
    public void Pinned_and_explicit_memories_resist_weak_inferred_updates()
    {
        var pinned = Create("User lives in Prague.", importance: 0.9m, confidence: 0.9m);
        pinned.Pin();

        var inferred = Create("User lives in Brno.", importance: 0.6m, confidence: 0.6m);
        Assert.False(pinned.CanBeSupersededBy(inferred));

        var explicitMemory = Create("User name is Matej.", importance: 0.8m, confidence: 0.95m, origin: MemoryOrigin.Explicit);
        Assert.False(explicitMemory.CanBeSupersededBy(inferred));

        var stronger = Create("User name is Matej Zavadil.", importance: 0.9m, confidence: 0.99m, origin: MemoryOrigin.Explicit);
        Assert.True(explicitMemory.CanBeSupersededBy(stronger));
    }

    [Fact]
    public void Forget_archives_and_unpins()
    {
        var memory = Create("User likes tea.");
        memory.Pin();
        memory.Forget();

        Assert.Equal(MemoryStatus.Archived, memory.Status);
        Assert.False(memory.IsPinned);
        Assert.False(memory.IsEffective(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Identity_facts_are_core()
    {
        var preference = Create("User prefers dark mode.", importance: 0.8m);
        var chit = Create("User said hello.", type: MemoryType.Summary, scope: MemoryScope.Conversation, importance: 0.9m);
        var constraint = Create("Do not use emojis.", type: MemoryType.Constraint, importance: 0.4m);

        Assert.True(MemoryStability.IsCore(preference, 0.75m));
        Assert.False(MemoryStability.IsCore(chit, 0.75m));
        Assert.True(MemoryStability.IsCore(constraint, 0.75m));
    }

    private static Memory Create(
        string content,
        MemoryType type = MemoryType.Preference,
        MemoryScope scope = MemoryScope.User,
        decimal importance = 0.5m,
        decimal confidence = 0.5m,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validUntil = null,
        MemoryOrigin origin = MemoryOrigin.Inferred)
    {
        return new Memory(
            "user-1",
            ConversationId,
            scope,
            content,
            type,
            importance,
            confidence,
            validFrom: validFrom,
            validUntil: validUntil,
            origin: origin);
    }
}
