namespace Memory.Application.OpenAiCompatible;

using Memory.Application.Agent;

public interface IOpenAiCompatibleChatService
{
    Task<OpenAiCompatibleChatResult> CompleteAsync(
        OpenAiCompatibleChatCommand command,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AgentChatStreamChunk> StreamAsync(
        OpenAiCompatibleChatCommand command,
        CancellationToken cancellationToken = default);
}
