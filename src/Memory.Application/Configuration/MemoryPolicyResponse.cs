namespace Memory.Application.Configuration;

public sealed record MemoryPolicyThresholds(
    decimal MinPersistConfidence,
    decimal MinPersistImportance,
    decimal AutoPromoteConfidence,
    decimal AutoPromoteImportance,
    int AutoPromoteEvidenceCount,
    decimal ContradictionConfidenceThreshold,
    int ContradictionCandidateLimit,
    float SimilarityThreshold,
    double MinRelevantSimilarity,
    double MinRelevantScore,
    int MaxRelevantMemories,
    decimal CoreImportanceThreshold,
    int CoreMemoryLimit);

public sealed record MemoryPolicyResponse(
    MemoryPolicyKind Policy,
    string Summary,
    IReadOnlyList<string> StagingEffects,
    IReadOnlyList<string> RecallEffects,
    MemoryPolicyThresholds Thresholds,
    IReadOnlyList<MemoryPolicyKind> Available);

public sealed record SetMemoryPolicyRequest(MemoryPolicyKind Policy);
