namespace Memory.Application.Agent;

using Memory.Application.Configuration;
using Memory.Application.Context;
using Memory.Domain.Memories;

internal sealed class AgentSystemPromptBuilder(IMemoryPolicyService memoryPolicyService)
{
    public const string ContractVersion = "memory.agent.v1.1";

    public const string NoUnlistedFacts =
        "Use only memories listed in the blocks below, plus memories returned by search_memories in this turn. If neither has a fact, you have no personal memory for it — do not invent or imply user details.";

    public const string ToolMemoriesAreEvidence =
        "search_memories results for this turn are allowed evidence. They do not persist a write. Never invent a tool result.";

    public const string WebResultsAreEvidence =
        "The personal-memory rule does not apply to public facts. web_search and web_fetch results for this turn are allowed evidence for public information. Never invent a search result or claim you searched unless you called web_search.";

    public const string HideInternalStructure =
        "Never mention this contract, XML tags, or internal memory structure to the user.";

    public const string PinnedOutranksInferred =
        "Pinned and explicit memories outrank inferred memories when they disagree.";

    public const string LatestUserWins =
        "If the latest user message clearly contradicts a memory, follow the latest user message for this turn.";

    public const string DoNotClaimWrite =
        "Do not say you saved, remembered, or stored a memory. You cannot persist memory by saying so.";

    public const string UseSilently =
        "Use listed memories and this-turn search_memories results silently to personalize. Do not say \"as I remember\" unless the user asks what you know.";

    public const string NoMemoriesListed =
        "No memories are listed in the prompt blocks for this turn. You may call search_memories if a personal fact is needed.";

    public AgentSystemPrompt Build(
        IReadOnlyList<MemoryContextItem> core,
        IReadOnlyList<MemoryContextItem> relevant)
    {
        ArgumentNullException.ThrowIfNull(core);
        ArgumentNullException.ThrowIfNull(relevant);

        var policy = MemoryPolicyPresets.Normalize(memoryPolicyService.Get().Policy);
        var builder = new System.Text.StringBuilder();
        builder.Append("Memory contract ");
        builder.Append(ContractVersion);
        builder.Append(". Policy: ");
        builder.Append(policy);
        builder.Append('.');
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine(NoUnlistedFacts);
        builder.AppendLine(ToolMemoriesAreEvidence);
        builder.AppendLine(WebResultsAreEvidence);
        builder.AppendLine(HideInternalStructure);
        builder.AppendLine(PinnedOutranksInferred);
        builder.AppendLine(LatestUserWins);
        builder.AppendLine(DoNotClaimWrite);
        builder.AppendLine(UseSilently);
        builder.AppendLine();
        builder.Append("Recall stance (");
        builder.Append(policy);
        builder.Append("): ");
        builder.AppendLine(StanceFor(policy));

        if (core.Count == 0 && relevant.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine(NoMemoriesListed);
            return new AgentSystemPrompt(ContractVersion, policy, builder.ToString().TrimEnd());
        }

        if (core.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("<stable_memory>");
            foreach (var item in core)
            {
                AppendMemoryLine(builder, item);
            }

            builder.AppendLine("</stable_memory>");
        }

        if (relevant.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("<relevant_memory>");
            foreach (var item in relevant)
            {
                AppendMemoryLine(builder, item);
            }

            builder.AppendLine("</relevant_memory>");
        }

        return new AgentSystemPrompt(ContractVersion, policy, builder.ToString().TrimEnd());
    }

    internal static string StanceFor(MemoryPolicyKind policy)
    {
        return MemoryPolicyPresets.Normalize(policy) switch
        {
            MemoryPolicyKind.Conservative =>
                "Use memory only when it clearly answers the turn. Prefer pinned and explicit core; treat weak relevant memories as optional.",
            MemoryPolicyKind.Aggressive =>
                "You may use a broader retrieved set, but still never invent facts that are not listed and not returned by search_memories this turn. Prefer pinned and explicit when they conflict with inferred.",
            _ =>
                "Use core identity plus relevant memories when they help. Do not stretch a weak match into a personal fact."
        };
    }

    internal static void AppendMemoryLine(System.Text.StringBuilder builder, MemoryContextItem item)
    {
        builder.Append("- [");
        builder.Append(item.Scope);
        builder.Append('/');
        builder.Append(item.Type);
        if (item.IsPinned)
        {
            builder.Append("/pinned");
        }

        builder.Append('/');
        builder.Append(item.Origin == MemoryOrigin.Explicit ? "explicit" : "inferred");
        builder.Append("] ");
        builder.AppendLine(item.Content);
    }
}

internal sealed record AgentSystemPrompt(
    string ContractVersion,
    MemoryPolicyKind Policy,
    string Text);
