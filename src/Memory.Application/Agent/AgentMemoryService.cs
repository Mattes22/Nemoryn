namespace Memory.Application.Agent;

using Memory.Application.Abstractions.Persistence;
using Memory.Application.Context;
using Memory.Application.Exceptions;
using Memory.Application.Memories;

internal sealed class AgentMemoryService(
    IMemoryStore memoryStore,
    IMemoryService memoryService,
    AgentSystemPromptBuilder promptBuilder) : IAgentMemoryService
{
    public async Task<PrepareAgentTurnResponse> PrepareTurnAsync(
        Guid conversationId,
        PrepareAgentTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        var conversation = await memoryStore.GetConversationAsync(conversationId, cancellationToken)
            ?? throw new NotFoundException("Conversation was not found.");

        var stable = await memoryService.RetrieveStableAsync(
            conversationId,
            new SearchMemoriesRequest(request.UserMessage ?? string.Empty, request.Limit),
            cancellationToken);

        var coreItems = stable.Core.Select(item => ToItem(item, "Core", MemoryContextDiagnostics.ExplainCoreSelection(item))).ToArray();
        var relevantItems = stable.Relevant.Select(item => ToItem(item, "Relevant", MemoryContextDiagnostics.ExplainRelevantSelection(item))).ToArray();

        var recentCount = request.RecentMessageCount is > 0 ? Math.Min(request.RecentMessageCount.Value, 40) : 12;
        var recent = await memoryStore.GetRecentMessagesAsync(conversationId, recentCount, cancellationToken);
        var recentMessages = recent
            .Select(message => new AgentRecentMessage(
                message.Id,
                message.Role,
                message.Content,
                message.SequenceNumber,
                message.OccurredAt))
            .ToArray();

        var prompt = promptBuilder.Build(coreItems, relevantItems);

        return new PrepareAgentTurnResponse(
            conversation.Id,
            conversation.OwnerId,
            prompt.Text,
            prompt.ContractVersion,
            prompt.Policy,
            coreItems,
            relevantItems,
            recentMessages);
    }

    private static MemoryContextItem ToItem(RankedMemory item, string selectionKind, string selectionReason)
    {
        return new MemoryContextItem(
            item.Memory.Id,
            item.Memory.Content,
            item.Memory.Type,
            item.Memory.Scope,
            item.Memory.Origin,
            item.Memory.IsPinned,
            item.Memory.Importance,
            item.Memory.Confidence,
            item.Similarity,
            item.Score,
            selectionKind,
            selectionReason,
            item.Memory.SourceSummary,
            item.Memory.ValidFrom,
            item.Memory.ValidUntil);
    }
}
