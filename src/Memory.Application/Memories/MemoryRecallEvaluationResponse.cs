namespace Memory.Application.Memories;

using Memory.Application.Configuration;
using Memory.Application.Context;
using Memory.Domain.Memories;

public sealed record MemoryRecallSelectedMemory(
    int Rank,
    MemoryContextItem Memory,
    MemoryScoreBreakdown ScoreBreakdown);

public sealed record MemoryRecallExpectationResult(
    Guid? MemoryId,
    string? ExpectedContentContains,
    bool Found,
    int? Rank,
    string? SelectionKind,
    string? MissReason,
    string? Content,
    MemoryType? Type,
    MemoryScope? Scope,
    double? Score,
    double? Similarity,
    MemoryScoreBreakdown? ScoreBreakdown);

public sealed record MemoryRecallCaseResult(
    string? Id,
    string Query,
    bool HasExpectation,
    bool Hit,
    int? BestExpectedRank,
    double? ReciprocalRank,
    IReadOnlyList<MemoryRecallSelectedMemory> Selected,
    IReadOnlyList<MemoryRecallExpectationResult> Expectations);

public sealed record MemoryRecallThresholds(
    MemoryPolicyKind Policy,
    double MinRelevantSimilarity,
    double MinRelevantScore,
    int MaxRelevantMemories,
    int CoreMemoryLimit,
    decimal CoreImportanceThreshold,
    bool EmbeddingAvailable);

public sealed record MemoryRecallSummary(
    int CaseCount,
    int EvaluableCaseCount,
    int Hits,
    int Misses,
    double? HitRate,
    double? MeanReciprocalRank);

public sealed record MemoryRecallEvaluationResponse(
    Guid ConversationId,
    string OwnerId,
    MemoryRecallThresholds Thresholds,
    MemoryRecallSummary Summary,
    IReadOnlyList<MemoryRecallCaseResult> Cases);
