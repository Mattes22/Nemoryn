namespace Memory.Application.Configuration;

using Memory.Domain.Memories;

public sealed class MemoryAiOptions
{
    public const string SectionName = "MemoryAi";
    public const int DefaultEmbeddingDimensions = 768;
    public const int SupportedEmbeddingDimensions = 768;

    public string Provider { get; set; } = "Ollama";
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string ChatModel { get; set; } = "llama3.1";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public int EmbeddingDimensions { get; set; } = DefaultEmbeddingDimensions;
    public float SimilarityThreshold { get; set; } = 0.80f;
    public double MinRelevantSimilarity { get; set; } = 0.62d;
    public double MinRelevantScore { get; set; } = 0.70d;
    public int MaxRelevantMemories { get; set; } = 8;
    public int RecentMessageWindow { get; set; } = 20;
    public int SearchLimit { get; set; } = 10;
    public decimal MinPersistConfidence { get; set; } = MemoryStability.DefaultMinPersistConfidence;
    public decimal MinPersistImportance { get; set; } = MemoryStability.DefaultMinPersistImportance;
    public decimal AutoPromoteConfidence { get; set; } = 0.82m;
    public decimal AutoPromoteImportance { get; set; } = 0.65m;
    public int AutoPromoteEvidenceCount { get; set; } = 2;
    public decimal ContradictionConfidenceThreshold { get; set; } = 0.72m;
    public int ContradictionCandidateLimit { get; set; } = 5;
    public decimal CoreImportanceThreshold { get; set; } = MemoryStability.DefaultCoreImportanceThreshold;
    public int CoreMemoryLimit { get; set; } = 12;
    public MemoryPolicyKind Policy { get; set; } = MemoryPolicyKind.Balanced;
    public int RetentionBatchSize { get; set; } = 50;
    public int RetentionPollSeconds { get; set; } = 900;
    public int StaleCandidateAgeDays { get; set; } = 14;
    public int CompletedIngestionJobAgeDays { get; set; } = 7;
}
