namespace Memory.Application.Agent;

public interface IAgentMemoryService
{
    Task<PrepareAgentTurnResponse> PrepareTurnAsync(
        Guid conversationId,
        PrepareAgentTurnRequest request,
        CancellationToken cancellationToken = default);
}
