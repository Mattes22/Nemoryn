namespace Memory.Application.Memories;

using Memory.Domain.Memories;

public static class MemoryRanker
{
    public static double Score(
        Memory memory,
        double? similarity,
        DateTimeOffset now)
    {
        return Describe(memory, similarity, now).Score;
    }

    public static MemoryScoreBreakdown Describe(
        Memory memory,
        double? similarity,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(memory);

        return Describe(
            memory.Importance,
            memory.Confidence,
            memory.UpdatedAt,
            memory.Type,
            memory.Origin,
            memory.IsPinned,
            similarity,
            now);
    }

    public static MemoryScoreBreakdown Describe(
        MemoryResponse memory,
        double? similarity,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(memory);

        return Describe(
            memory.Importance,
            memory.Confidence,
            memory.UpdatedAt,
            memory.Type,
            memory.Origin,
            memory.IsPinned,
            similarity,
            now);
    }

    public static MemoryScoreBreakdown Describe(
        decimal importance,
        decimal confidence,
        DateTimeOffset updatedAt,
        MemoryType type,
        MemoryOrigin origin,
        bool isPinned,
        double? similarity,
        DateTimeOffset now)
    {
        var similarityScore = similarity ?? (double)importance;
        var recency = Recency(updatedAt, now);
        var typeBoost = TypeBoost(type);
        var stabilityBoost = 0d;
        if (isPinned)
        {
            stabilityBoost += 0.16;
        }
        else if (origin == MemoryOrigin.Explicit)
        {
            stabilityBoost += 0.06;
        }

        var similarityComponent = 0.50 * similarityScore;
        var importanceComponent = 0.22 * (double)importance;
        var confidenceComponent = 0.14 * (double)confidence;
        var recencyComponent = 0.14 * recency;
        var score = similarityComponent
            + importanceComponent
            + confidenceComponent
            + recencyComponent
            + typeBoost
            + stabilityBoost;

        return new MemoryScoreBreakdown(
            score,
            similarity,
            similarityScore,
            similarityComponent,
            importanceComponent,
            confidenceComponent,
            recencyComponent,
            recency,
            typeBoost,
            stabilityBoost);
    }

    public static double ToSimilarity(double cosineDistance)
    {
        return Math.Clamp(1d - cosineDistance, 0d, 1d);
    }

    public static double EmbeddingSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        return ToSimilarity(CosineDistance(left, right));
    }

    public static double CosineDistance(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        if (left.Count != right.Count)
        {
            return 2d;
        }

        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;

        for (var index = 0; index < left.Count; index++)
        {
            dot += left[index] * right[index];
            leftNorm += left[index] * left[index];
            rightNorm += right[index] * right[index];
        }

        var denominator = Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm);
        if (denominator == 0)
        {
            return 1d;
        }

        return 1d - (dot / denominator);
    }

    private static double Recency(DateTimeOffset updatedAt, DateTimeOffset now)
    {
        var ageDays = Math.Max(0, (now - updatedAt).TotalDays);
        return Math.Exp(-ageDays / 30d);
    }

    private static double TypeBoost(MemoryType type)
    {
        return type switch
        {
            MemoryType.Constraint => 0.08,
            MemoryType.Preference => 0.06,
            MemoryType.Fact => 0.04,
            MemoryType.Goal => 0.03,
            MemoryType.Relationship => 0.03,
            _ => 0d
        };
    }
}
