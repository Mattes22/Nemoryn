namespace Memory.Application.Context;

using Memory.Application.Agent;
using Memory.Application.Memories;
using Memory.Domain.Memories;

internal sealed class ContextBuilder(
    IMemoryService memoryService,
    AgentSystemPromptBuilder promptBuilder) : IContextBuilder
{
    public async Task<MemoryContextResponse> BuildAsync(
        Guid conversationId,
        BuildMemoryContextRequest request,
        CancellationToken cancellationToken = default)
    {
        var stable = await memoryService.RetrieveStableAsync(
            conversationId,
            new SearchMemoriesRequest(request.Query ?? string.Empty, request.Limit),
            cancellationToken);

        var items = stable.All
            .Select(item => new MemoryContextItem(
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
                "Context",
                MemoryContextDiagnostics.ExplainContextSelection(item),
                item.Memory.SourceSummary,
                item.Memory.ValidFrom,
                item.Memory.ValidUntil))
            .ToArray();

        var core = items.Where(item => item.IsPinned || item.Origin == MemoryOrigin.Explicit).ToArray();
        var relevant = items.Where(item => !item.IsPinned && item.Origin != MemoryOrigin.Explicit).ToArray();

        return new MemoryContextResponse(
            conversationId,
            promptBuilder.Build(core, relevant).Text,
            items);
    }
}

internal static class MemoryContextDiagnostics
{
    public static string ExplainCoreSelection(RankedMemory item)
    {
        if (item.Memory.IsPinned)
        {
            return "Core memory because it is pinned.";
        }

        if (item.Memory.Type == MemoryType.Constraint)
        {
            return "Core memory because user constraints are always treated as stable.";
        }

        if (item.Memory.Origin == MemoryOrigin.Explicit)
        {
            return "Core memory because it is an explicit high-importance user memory.";
        }

        return "Core memory because it is a high-importance stable user memory.";
    }

    public static string ExplainRelevantSelection(RankedMemory item)
    {
        if (item.Similarity is not null)
        {
            return $"Relevant memory selected by semantic similarity {item.Similarity.Value:0.00}, weighted by importance and confidence.";
        }

        return "Relevant memory selected by fallback stability score because semantic search was unavailable or no query was supplied.";
    }

    public static string ExplainContextSelection(RankedMemory item)
    {
        if (item.Memory.IsPinned)
        {
            return "Pinned memory is always included as stable context.";
        }

        if (item.Similarity is not null)
        {
            return $"Selected by semantic similarity {item.Similarity.Value:0.00}, then weighted by importance and confidence.";
        }

        return "Selected by stability score from importance, confidence, recency, origin and type.";
    }
}
