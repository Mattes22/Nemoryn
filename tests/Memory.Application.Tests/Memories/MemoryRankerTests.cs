namespace Memory.Application.Tests.Memories;

using Memory.Application.Memories;
using Memory.Domain.Memories;

public sealed class MemoryRankerTests
{
    [Fact]
    public void Higher_similarity_and_importance_rank_first()
    {
        var now = DateTimeOffset.UtcNow;
        var weak = new Memory(
            "user-1",
            Guid.NewGuid(),
            MemoryScope.User,
            "User once mentioned tea.",
            MemoryType.Summary,
            0.2m,
            0.2m);
        var strong = new Memory(
            "user-1",
            Guid.NewGuid(),
            MemoryScope.User,
            "User prefers coffee.",
            MemoryType.Preference,
            0.9m,
            0.9m);

        var weakScore = MemoryRanker.Score(weak, 0.4, now);
        var strongScore = MemoryRanker.Score(strong, 0.95, now);

        Assert.True(strongScore > weakScore);
    }

    [Fact]
    public void Describe_matches_score_and_sums_components()
    {
        var now = DateTimeOffset.UtcNow;
        var memory = new Memory(
            "user-1",
            Guid.NewGuid(),
            MemoryScope.User,
            "User prefers coffee.",
            MemoryType.Preference,
            0.9m,
            0.8m,
            origin: MemoryOrigin.Explicit);

        var breakdown = MemoryRanker.Describe(memory, 0.92, now);

        Assert.Equal(MemoryRanker.Score(memory, 0.92, now), breakdown.Score, 10);
        Assert.Equal(0.92, breakdown.Similarity);
        Assert.Equal(
            breakdown.SimilarityComponent
            + breakdown.ImportanceComponent
            + breakdown.ConfidenceComponent
            + breakdown.RecencyComponent
            + breakdown.TypeBoost
            + breakdown.StabilityBoost,
            breakdown.Score,
            10);
        Assert.Equal(0.06, breakdown.StabilityBoost);
    }
}
