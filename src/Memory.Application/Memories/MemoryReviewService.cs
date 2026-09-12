namespace Memory.Application.Memories;

using Memory.Application.Abstractions.Persistence;
using Memory.Application.Exceptions;

internal sealed class MemoryReviewService(
    IMemoryStore memoryStore,
    IMemoryCandidateService memoryCandidateService,
    IMemoryConflictService memoryConflictService,
    IMemoryCleanupService memoryCleanupService) : IMemoryReviewService
{
    public async Task<MemoryReviewResponse> GetAsync(
        string ownerId,
        Guid? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(ownerId));
        }

        var normalizedOwnerId = ownerId.Trim();
        IReadOnlyList<MemoryResponse> memories;
        IReadOnlyList<MemoryCandidateResponse> candidates;
        IReadOnlyList<MemoryConflictResponse> conflicts;

        if (conversationId is Guid conversation)
        {
            var existing = await memoryStore.GetConversationAsync(conversation, cancellationToken)
                ?? throw new NotFoundException("Conversation was not found.");
            if (!string.Equals(existing.OwnerId, normalizedOwnerId, StringComparison.Ordinal))
            {
                throw new ConflictException("Conversation does not belong to this owner.");
            }

            var active = await memoryStore.GetActiveMemoriesAsync(
                MemoryLookup.ForRetrieval(normalizedOwnerId, conversation),
                cancellationToken);
            memories = active.Select(MemoryService.ToResponse).ToArray();
            candidates = await memoryCandidateService.GetPendingForConversationAsync(
                conversation,
                cancellationToken);
            conflicts = await memoryConflictService.GetPendingForConversationAsync(
                conversation,
                cancellationToken);
        }
        else
        {
            var active = await memoryStore.GetActiveMemoriesForOwnerAsync(
                normalizedOwnerId,
                cancellationToken);
            memories = active.Select(MemoryService.ToResponse).ToArray();
            candidates = await memoryCandidateService.GetPendingForOwnerAsync(
                normalizedOwnerId,
                cancellationToken);
            conflicts = await memoryConflictService.GetPendingForOwnerAsync(
                normalizedOwnerId,
                cancellationToken);
        }

        var cleanup = await memoryCleanupService.PreviewAsync(normalizedOwnerId, cancellationToken);
        if (conversationId is not null)
        {
            var memoryIds = memories.Select(memory => memory.Id).ToHashSet();
            var suggestions = cleanup.Suggestions
                .Where(suggestion => memoryIds.Contains(suggestion.MemoryId))
                .ToArray();
            cleanup = cleanup with
            {
                ScannedCount = memories.Count,
                Suggestions = suggestions
            };
        }

        return new MemoryReviewResponse(
            normalizedOwnerId,
            conversationId,
            candidates.Count + conflicts.Count + cleanup.Suggestions.Count,
            memories,
            candidates,
            conflicts,
            cleanup);
    }
}
