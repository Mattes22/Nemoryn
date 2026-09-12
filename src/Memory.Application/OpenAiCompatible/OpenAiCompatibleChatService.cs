namespace Memory.Application.OpenAiCompatible;

using Memory.Application.Agent;
using Memory.Application.Conversations;
using Memory.Application.Exceptions;

internal sealed class OpenAiCompatibleChatService(
    IConversationService conversationService,
    IAgentChatService agentChatService) : IOpenAiCompatibleChatService
{
    public async Task<OpenAiCompatibleChatResult> CompleteAsync(
        OpenAiCompatibleChatCommand command,
        CancellationToken cancellationToken = default)
    {
        var ownerId = Require(command.OwnerId, "Owner id is required.");
        var conversationKey = Require(command.ConversationKey, "Conversation id is required.");
        var userMessage = Require(command.UserMessage, "User message is required.");

        var conversation = await ResolveConversationAsync(ownerId, conversationKey, cancellationToken);
        var chat = await agentChatService.ChatAsync(
            conversation.Id,
            new AgentChatRequest(
                userMessage,
                command.MemoryLimit,
                command.RecentMessageCount,
                ChatModel: command.ChatModel),
            cancellationToken);

        return new OpenAiCompatibleChatResult(conversation, chat);
    }

    public async IAsyncEnumerable<AgentChatStreamChunk> StreamAsync(
        OpenAiCompatibleChatCommand command,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var ownerId = Require(command.OwnerId, "Owner id is required.");
        var conversationKey = Require(command.ConversationKey, "Conversation id is required.");
        var userMessage = Require(command.UserMessage, "User message is required.");

        var conversation = await ResolveConversationAsync(ownerId, conversationKey, cancellationToken);
        await foreach (var chunk in agentChatService.ChatStreamingAsync(
            conversation.Id,
            new AgentChatRequest(
                userMessage,
                command.MemoryLimit,
                command.RecentMessageCount,
                ChatModel: command.ChatModel),
            cancellationToken))
        {
            yield return chunk;
        }
    }

    private async Task<ConversationResponse> ResolveConversationAsync(
        string ownerId,
        string conversationKey,
        CancellationToken cancellationToken)
    {
        if (Guid.TryParse(conversationKey, out var conversationId))
        {
            var existing = await conversationService.GetAsync(conversationId, cancellationToken);
            if (existing is not null)
            {
                if (!string.Equals(existing.OwnerId, ownerId, StringComparison.Ordinal))
                {
                    throw new ConflictException("Conversation does not belong to this owner.");
                }

                return existing;
            }
        }

        var result = await conversationService.UpsertByExternalIdAsync(
            conversationKey,
            new UpsertConversationRequest(ownerId, Title: "OpenAI compatible chat"),
            cancellationToken);

        return result.Conversation;
    }

    private static string Require(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message);
        }

        return value.Trim();
    }
}
