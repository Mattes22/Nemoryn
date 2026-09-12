namespace Memory.Application.Agent;

using Memory.Application.Configuration;
using Memory.Application.Context;

public sealed record PrepareAgentTurnResponse(
    Guid ConversationId,
    string OwnerId,
    string SystemPromptBlock,
    string PromptContractVersion,
    MemoryPolicyKind Policy,
    IReadOnlyList<MemoryContextItem> CoreMemories,
    IReadOnlyList<MemoryContextItem> RelevantMemories,
    IReadOnlyList<AgentRecentMessage> RecentMessages);
