namespace Memory.Application.Memories;

public sealed record MemoryScoreBreakdown(
    double Score,
    double? Similarity,
    double SimilarityScore,
    double SimilarityComponent,
    double ImportanceComponent,
    double ConfidenceComponent,
    double RecencyComponent,
    double Recency,
    double TypeBoost,
    double StabilityBoost);
