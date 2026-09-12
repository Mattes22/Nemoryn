namespace Memory.Application.OpenAiCompatible;

using Memory.Application.Agent;
using Memory.Application.Conversations;

public sealed record OpenAiCompatibleChatResult(
    ConversationResponse Conversation,
    AgentChatResponse Chat);
