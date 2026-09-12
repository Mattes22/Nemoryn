namespace Memory.Application.Abstractions.Persistence;

using Memory.Application.Owners;
using Memory.Domain.Conversations;
using Memory.Domain.Ingestion;
using Memory.Domain.Memories;
using Memory.Domain.Tools;
using MemoryEntity = Memory.Domain.Memories.Memory;

public interface IMemoryStore
{
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
    Task<bool> ConversationExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Conversation?> GetConversationAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Conversation?> GetConversationForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Conversation?> GetConversationByExternalIdAsync(
        string ownerId,
        string externalId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Conversation>> GetConversationsForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OwnerSummary>> ListOwnersAsync(CancellationToken cancellationToken = default);
    Task<Message?> GetMessageAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Message>> GetRecentMessagesAsync(
        Guid conversationId,
        int take,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryIngestionJob>> GetIngestionJobsForConversationAsync(
        Guid conversationId,
        int take,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryIngestionJob>> GetIngestionJobsForOwnerAsync(
        string ownerId,
        int take,
        CancellationToken cancellationToken = default);
    Task<MemoryEntity?> GetMemoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MemoryEntity?> GetActiveMemoryByFingerprintAsync(
        string fingerprint,
        CancellationToken cancellationToken = default);
    Task<MemoryCandidate?> GetPendingMemoryCandidateByFingerprintAsync(
        string fingerprint,
        CancellationToken cancellationToken = default);
    Task<MemoryCandidate?> GetMemoryCandidateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryCandidate>> GetPendingMemoryCandidatesForConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryCandidate>> GetPendingMemoryCandidatesForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryEvidence>> GetMemoryEvidenceAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default);
    Task<bool> MemoryEvidenceExistsAsync(
        Guid candidateId,
        Guid sourceMessageId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryConflict>> GetPendingMemoryConflictsForConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryConflict>> GetPendingMemoryConflictsForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default);
    Task<MemoryConflict?> GetMemoryConflictAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MemoryConflict?> GetPendingMemoryConflictAsync(
        Guid candidateId,
        Guid conflictingMemoryId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryConflict>> GetPendingMemoryConflictsForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryEntity>> GetActiveMemoriesAsync(
        MemoryLookup lookup,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryEntity>> GetActiveMemoriesForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryEntity>> GetMemoriesByIdsForOwnerAsync(
        string ownerId,
        IReadOnlyCollection<Guid> memoryIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemorySimilarity>> SearchActiveMemoriesAsync(
        MemoryLookup lookup,
        IReadOnlyList<float> embedding,
        int limit,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryAuditLog>> GetMemoryAuditLogsAsync(
        string ownerId,
        Guid? memoryId,
        Guid? conversationId,
        int take,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ToolAuditLog>> GetToolAuditLogsAsync(
        string ownerId,
        Guid? conversationId,
        int take,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryEntity>> GetExpiredActiveMemoriesAsync(
        DateTimeOffset now,
        int take,
        CancellationToken cancellationToken = default);
    Task<DatabaseRuntimeInfo> GetDatabaseRuntimeAsync(CancellationToken cancellationToken = default);
    Task<RuntimeQueueCounts> GetRuntimeQueueCountsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryCandidate>> GetPendingCandidatesForRetentionAsync(
        DateTimeOffset now,
        DateTimeOffset? staleBefore,
        decimal maxConfidence,
        decimal maxImportance,
        int take,
        CancellationToken cancellationToken = default);
    Task<int> RemoveCompletedIngestionJobsAsync(
        DateTimeOffset cutoff,
        int take,
        CancellationToken cancellationToken = default);
    void AddConversation(Conversation conversation);
    void AddMessage(Message message);
    void AddMemory(MemoryEntity memory);
    void AddMemoryCandidate(MemoryCandidate candidate);
    void AddMemoryEvidence(MemoryEvidence evidence);
    void AddMemoryConflict(MemoryConflict conflict);
    void AddIngestionJob(MemoryIngestionJob job);
    void AddMemoryAuditLog(MemoryAuditLog entry);
    void AddToolAuditLog(ToolAuditLog entry);
    Task<MemoryIngestionJob?> ClaimNextIngestionJobAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
