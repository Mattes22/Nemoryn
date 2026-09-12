namespace Memory.Application.Configuration;

public static class MemoryPolicyPresets
{
    public static readonly IReadOnlyList<MemoryPolicyKind> Available =
    [
        MemoryPolicyKind.Conservative,
        MemoryPolicyKind.Balanced,
        MemoryPolicyKind.Aggressive
    ];

    public static MemoryPolicyThresholds ThresholdsFor(MemoryPolicyKind policy)
    {
        return policy switch
        {
            MemoryPolicyKind.Conservative => new MemoryPolicyThresholds(
                MinPersistConfidence: 0.65m,
                MinPersistImportance: 0.45m,
                AutoPromoteConfidence: 0.90m,
                AutoPromoteImportance: 0.75m,
                AutoPromoteEvidenceCount: 3,
                ContradictionConfidenceThreshold: 0.62m,
                ContradictionCandidateLimit: 8,
                SimilarityThreshold: 0.86f,
                MinRelevantSimilarity: 0.70d,
                MinRelevantScore: 0.78d,
                MaxRelevantMemories: 6,
                CoreImportanceThreshold: 0.82m,
                CoreMemoryLimit: 8),
            MemoryPolicyKind.Aggressive => new MemoryPolicyThresholds(
                MinPersistConfidence: 0.45m,
                MinPersistImportance: 0.25m,
                AutoPromoteConfidence: 0.72m,
                AutoPromoteImportance: 0.50m,
                AutoPromoteEvidenceCount: 1,
                ContradictionConfidenceThreshold: 0.82m,
                ContradictionCandidateLimit: 3,
                SimilarityThreshold: 0.74f,
                MinRelevantSimilarity: 0.52d,
                MinRelevantScore: 0.60d,
                MaxRelevantMemories: 12,
                CoreImportanceThreshold: 0.65m,
                CoreMemoryLimit: 16),
            _ => new MemoryPolicyThresholds(
                MinPersistConfidence: 0.55m,
                MinPersistImportance: 0.35m,
                AutoPromoteConfidence: 0.82m,
                AutoPromoteImportance: 0.65m,
                AutoPromoteEvidenceCount: 2,
                ContradictionConfidenceThreshold: 0.72m,
                ContradictionCandidateLimit: 5,
                SimilarityThreshold: 0.80f,
                MinRelevantSimilarity: 0.62d,
                MinRelevantScore: 0.70d,
                MaxRelevantMemories: 8,
                CoreImportanceThreshold: 0.75m,
                CoreMemoryLimit: 12)
        };
    }

    public static void Apply(MemoryAiOptions options, MemoryPolicyKind policy)
    {
        ArgumentNullException.ThrowIfNull(options);

        var resolved = Normalize(policy);
        var preset = ThresholdsFor(resolved);
        options.Policy = resolved;
        options.MinPersistConfidence = preset.MinPersistConfidence;
        options.MinPersistImportance = preset.MinPersistImportance;
        options.AutoPromoteConfidence = preset.AutoPromoteConfidence;
        options.AutoPromoteImportance = preset.AutoPromoteImportance;
        options.AutoPromoteEvidenceCount = preset.AutoPromoteEvidenceCount;
        options.ContradictionConfidenceThreshold = preset.ContradictionConfidenceThreshold;
        options.ContradictionCandidateLimit = preset.ContradictionCandidateLimit;
        options.SimilarityThreshold = preset.SimilarityThreshold;
        options.MinRelevantSimilarity = preset.MinRelevantSimilarity;
        options.MinRelevantScore = preset.MinRelevantScore;
        options.MaxRelevantMemories = preset.MaxRelevantMemories;
        options.CoreImportanceThreshold = preset.CoreImportanceThreshold;
        options.CoreMemoryLimit = preset.CoreMemoryLimit;
    }

    public static MemoryPolicyResponse Describe(MemoryAiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var policy = Normalize(options.Policy);
        return Describe(policy, ReadThresholds(options));
    }

    public static MemoryPolicyResponse Describe(MemoryPolicyKind policy)
    {
        var resolved = Normalize(policy);
        return Describe(resolved, ThresholdsFor(resolved));
    }

    public static MemoryPolicyKind Normalize(MemoryPolicyKind policy)
    {
        return Enum.IsDefined(policy) && policy != default
            ? policy
            : MemoryPolicyKind.Balanced;
    }

    private static MemoryPolicyResponse Describe(MemoryPolicyKind policy, MemoryPolicyThresholds thresholds)
    {
        return policy switch
        {
            MemoryPolicyKind.Conservative => new MemoryPolicyResponse(
                policy,
                "Píše málo a do promptu pouští jen silné shody.",
                [
                    "Persist až od vyšší confidence/importance — slabé kandidáty ingest zahodí.",
                    "Auto-promote až po 3 evidencích a vyšším skóre.",
                    "Konflikty se otevírají dřív, i u slabších rozporů."
                ],
                [
                    "Přísnější similarity i score práh, méně relevantních pamětí.",
                    "Core je užší: do stabilního bloku jde jen silnější identita."
                ],
                thresholds,
                Available),
            MemoryPolicyKind.Aggressive => new MemoryPolicyResponse(
                policy,
                "Učí se rychleji a do promptu pouští víc pamětí.",
                [
                    "Slabší kandidáti se ukládají; auto-promote už z jedné evidence.",
                    "Konflikty jen u silných rozporů, jinak se paměť přepíše.",
                    "Blízké duplikáty se slučují ochotněji."
                ],
                [
                    "Nižší similarity/score práh, víc relevantních pamětí v promptu.",
                    "Core je širší: identita a preference se berou do stabilního bloku dřív."
                ],
                thresholds,
                Available),
            _ => new MemoryPolicyResponse(
                MemoryPolicyKind.Balanced,
                "Produkční kompromis mezi zápisem a recall.",
                [
                    "Auto-promote po 2 evidencích při běžném skóre.",
                    "Konflikty se otevírají u středně silných rozporů.",
                    "Duplikáty se slučují při similarity 0.80."
                ],
                [
                    "Hybrid core + relevant s prahy 0.62 / 0.70.",
                    "Do promptu jde až 8 relevantních pamětí vedle core identity."
                ],
                thresholds,
                Available)
        };
    }

    private static MemoryPolicyThresholds ReadThresholds(MemoryAiOptions options)
    {
        return new MemoryPolicyThresholds(
            options.MinPersistConfidence,
            options.MinPersistImportance,
            options.AutoPromoteConfidence,
            options.AutoPromoteImportance,
            options.AutoPromoteEvidenceCount,
            options.ContradictionConfidenceThreshold,
            options.ContradictionCandidateLimit,
            options.SimilarityThreshold,
            options.MinRelevantSimilarity,
            options.MinRelevantScore,
            options.MaxRelevantMemories,
            options.CoreImportanceThreshold,
            options.CoreMemoryLimit);
    }
}
