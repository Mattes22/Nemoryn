namespace Memory.Application.Tests.Agent;

using Memory.Application.Abstractions.AI;
using Memory.Application.Abstractions.Persistence;
using Memory.Application.Agent;
using Memory.Application.Conversations;
using Memory.Application.Tools;
using Memory.Application.Tests.Fakes;

internal static class AgentChatHarness
{
    public static AgentChatService Create(
        IConversationService conversations,
        IAgentMemoryService memory,
        IChatCompletionProvider chatProvider,
        TimeProvider? timeProvider = null,
        IEnumerable<ITool>? tools = null,
        IMemoryStore? store = null)
    {
        var time = timeProvider ?? TimeProvider.System;
        var registry = new ToolRegistry(tools ?? [new GetTimeTool(time)]);
        return new AgentChatService(
            conversations,
            memory,
            chatProvider,
            registry,
            new ToolRuntime(registry, new ToolAuditService(store ?? new FakeMemoryStore(), time)));
    }
}
