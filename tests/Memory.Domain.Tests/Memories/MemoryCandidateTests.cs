namespace Memory.Domain.Tests.Memories;

using Memory.Domain.Memories;

public sealed class MemoryCandidateTests
{
    private static readonly Guid ConversationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void AddEvidence_raises_scores_and_count()
    {
        var candidate = Create();
        candidate.AddEvidence(0.4m, 0.9m);

        Assert.Equal(1, candidate.EvidenceCount);
        Assert.Equal(0.6m, candidate.Importance);
        Assert.Equal(0.9m, candidate.Confidence);
        Assert.NotNull(candidate.LastEvidenceAt);
    }

    [Fact]
    public void Promote_and_reject_are_terminal()
    {
        var promoted = Create();
        promoted.Promote(Guid.NewGuid());
        Assert.Equal(MemoryCandidateStatus.Promoted, promoted.Status);
        Assert.Throws<InvalidOperationException>(() => promoted.Reject());

        var rejected = Create();
        rejected.Reject();
        Assert.Throws<InvalidOperationException>(() => rejected.AddEvidence(0.5m, 0.5m));
    }

    [Fact]
    public void MergeInto_marks_merged_and_rejects_self()
    {
        var candidate = Create();
        var targetId = Guid.NewGuid();
        candidate.MergeInto(targetId);

        Assert.Equal(MemoryCandidateStatus.Merged, candidate.Status);
        Assert.Equal(targetId, candidate.MergedIntoCandidateId);

        var other = Create();
        Assert.Throws<ArgumentException>(() => other.MergeInto(other.Id));
        Assert.Throws<InvalidOperationException>(() => candidate.MergeInto(Guid.NewGuid()));
    }

    private static MemoryCandidate Create()
    {
        return new MemoryCandidate(
            "user-1",
            ConversationId,
            MemoryScope.User,
            "User likes tea.",
            MemoryType.Preference,
            0.6m,
            0.7m);
    }
}

public sealed class MemoryConflictTests
{
    [Fact]
    public void Resolve_is_allowed_only_once()
    {
        var conflict = Create();
        conflict.MarkCandidateAccepted();

        Assert.Equal(MemoryConflictStatus.CandidateAccepted, conflict.Status);
        Assert.Throws<InvalidOperationException>(() => conflict.MarkExistingKept());
    }

    [Fact]
    public void Keep_existing_is_terminal()
    {
        var conflict = Create();
        conflict.MarkExistingKept();

        Assert.Equal(MemoryConflictStatus.ExistingKept, conflict.Status);
        Assert.Throws<InvalidOperationException>(() => conflict.MarkCandidateAccepted());
    }

    private static MemoryConflict Create()
    {
        return new MemoryConflict(
            "user-1",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            0.8m,
            "Contradiction.");
    }
}
