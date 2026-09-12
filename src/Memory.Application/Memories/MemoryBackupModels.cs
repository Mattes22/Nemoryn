namespace Memory.Application.Memories;

using Memory.Application.Configuration;
using Memory.Domain.Memories;

public sealed record MemoryExportConversation(
    Guid Id,
    string ExternalId,
    string? Title);

public sealed record MemoryExportMemory(
    Guid Id,
    Guid ConversationId,
    MemoryScope Scope,
    string Content,
    MemoryType Type,
    MemoryOrigin Origin,
    bool IsPinned,
    decimal Importance,
    decimal Confidence,
    string Fingerprint,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    string? SourceSummary,
    string? SourceMetadataJson,
    float[]? Embedding,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record MemoryExportCandidate(
    Guid Id,
    Guid ConversationId,
    MemoryScope Scope,
    string Content,
    MemoryType Type,
    decimal Importance,
    decimal Confidence,
    string Fingerprint,
    int EvidenceCount,
    DateTimeOffset? LastEvidenceAt,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record MemoryExportConflict(
    Guid Id,
    Guid ConversationId,
    Guid CandidateId,
    Guid ConflictingMemoryId,
    string CandidateFingerprint,
    string ConflictingMemoryFingerprint,
    decimal Confidence,
    string? Reason);

public sealed record MemoryExportAuditActionCount(
    MemoryAuditAction Action,
    int Count);

public sealed record MemoryExportAuditMetadata(
    int ExportedCount,
    IReadOnlyList<MemoryExportAuditActionCount> ByAction,
    DateTimeOffset? LastOccurredAt,
    IReadOnlyList<MemoryAuditLogResponse> Recent);

public sealed record MemoryProfileExport(
    string Format,
    DateTimeOffset ExportedAt,
    string OwnerId,
    MemoryPolicyKind? Policy,
    IReadOnlyList<MemoryExportConversation> Conversations,
    IReadOnlyList<MemoryExportMemory> Memories,
    IReadOnlyList<MemoryExportCandidate> Candidates,
    IReadOnlyList<MemoryExportConflict> Conflicts,
    MemoryExportAuditMetadata Audit)
{
    public const string FormatVersion = "memory.profile.v1";
}

public sealed record MemoryImportRequest(
    MemoryProfileExport Document,
    bool? DryRun = null,
    bool? SkipDuplicates = null,
    bool? PinExplicit = null);

public sealed record MemoryImportCounts(
    int Created,
    int Skipped,
    int Failed);

public sealed record MemoryImportIssue(
    string Kind,
    string? Fingerprint,
    string Reason);

public sealed record MemoryImportResult(
    bool DryRun,
    bool SkipDuplicates,
    bool PinExplicit,
    string OwnerId,
    string? SourceOwnerId,
    MemoryImportCounts Conversations,
    MemoryImportCounts Memories,
    MemoryImportCounts Candidates,
    MemoryImportCounts Conflicts,
    IReadOnlyList<MemoryImportIssue> Issues);
