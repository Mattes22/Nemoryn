namespace Memory.Application.Agent;

public interface IAgentChatService
{
    Task<AgentChatResponse> ChatAsync(
        Guid conversationId,
        AgentChatRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AgentChatStreamChunk> ChatStreamingAsync(
        Guid conversationId,
        AgentChatRequest request,
        CancellationToken cancellationToken = default);
}
