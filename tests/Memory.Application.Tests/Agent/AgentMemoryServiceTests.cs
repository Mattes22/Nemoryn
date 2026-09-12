namespace Memory.Application.Tests.Agent;

using Microsoft.Extensions.Options;
using Memory.Application.Agent;
using Memory.Application.Configuration;
using Memory.Application.Conversations;
using Memory.Application.Memories;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Conversations;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

public sealed class AgentMemoryServiceTests
{
    [Fact]
    public async Task Chat_saves_user_and_assistant_messages_with_memory_prompt()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        var pinned = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User prefers Czech.",
            MemoryType.Preference,
            0.95m,
            0.95m);
        pinned.Pin();
        store.AddConversation(conversation);
        store.AddMemory(pinned);

        var memoryService = new MemoryService(
            store,
            new FakeEmbeddingProvider { IsAvailable = false },
            Options.Create(new MemoryAiOptions { SearchLimit = 10, CoreMemoryLimit = 12 }));
        var memory = new AgentMemoryService(
            store,
            memoryService,
            new AgentSystemPromptBuilder(new FakeMemoryPolicyService()));
        var chatProvider = new FakeChatCompletionProvider { Response = "Budu odpovídat česky." };
        var conversations = new ConversationService(store);
        var chat = AgentChatHarness.Create(conversations, memory, chatProvider);

        var response = await chat.ChatAsync(
            conversation.Id,
            new AgentChatRequest("Odpověz mi.", 8, 12));

        Assert.Equal("Succeeded", response.Status);
        Assert.Equal("Fake", response.Provider);
        Assert.Equal("fake-chat", response.Model);
        Assert.Equal(MessageRole.User, response.UserMessage.Role);
        Assert.NotNull(response.AssistantMessage);
        Assert.Equal(MessageRole.Assistant, response.AssistantMessage.Role);
        Assert.Equal("Budu odpovídat česky.", response.AssistantMessage.Content);
        Assert.Single(store.Jobs);
        Assert.NotNull(chatProvider.LastRequest);
        Assert.Contains(chatProvider.LastRequest.Messages, message =>
            message.Role == "system" && message.Content.Contains("User prefers Czech.", StringComparison.Ordinal));
        Assert.Contains(chatProvider.LastRequest.Messages, message =>
            message.Role == "user" && message.Content == "Odpověz mi.");
        Assert.NotNull(response.UserMessage.IngestionJobId);
        Assert.Empty(response.ToolTrace);
        Assert.Contains(
            chatProvider.LastRequest.Tools ?? [],
            tool => tool.Name == "get_time");
    }

    [Fact]
    public async Task Chat_does_not_enqueue_ingestion_until_the_model_returns()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);

        var memoryService = new MemoryService(
            store,
            new FakeEmbeddingProvider { IsAvailable = false },
            Options.Create(new MemoryAiOptions { SearchLimit = 10, CoreMemoryLimit = 12 }));
        var memory = new AgentMemoryService(
            store,
            memoryService,
            new AgentSystemPromptBuilder(new FakeMemoryPolicyService()));
        var jobsDuringCompletion = -1;
        var chatProvider = new FakeChatCompletionProvider
        {
            Response = "Ahoj.",
            BeforeComplete = () => jobsDuringCompletion = store.Jobs.Count
        };
        var conversations = new ConversationService(store);
        var chat = AgentChatHarness.Create(conversations, memory, chatProvider);

        var response = await chat.ChatAsync(
            conversation.Id,
            new AgentChatRequest("Ahoj", 8, 12));

        Assert.Equal("Succeeded", response.Status);
        Assert.Equal(0, jobsDuringCompletion);
        Assert.Single(store.Jobs);
        Assert.NotNull(response.UserMessage.IngestionJobId);
        Assert.NotNull(chatProvider.LastRequest);
        Assert.Contains(chatProvider.LastRequest.Messages, message =>
            message.Role == "system"
            && message.Content.Contains(AgentSystemPromptBuilder.NoMemoriesListed, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ChatStreaming_yields_deltas_then_enqueues_ingestion()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);

        var memoryService = new MemoryService(
            store,
            new FakeEmbeddingProvider { IsAvailable = false },
            Options.Create(new MemoryAiOptions { SearchLimit = 10, CoreMemoryLimit = 12 }));
        var jobsDuringStream = -1;
        var chatProvider = new FakeChatCompletionProvider
        {
            Response = "Ahoj.",
            BeforeComplete = () => jobsDuringStream = store.Jobs.Count
        };
        var chat = AgentChatHarness.Create(
            new ConversationService(store),
            new AgentMemoryService(
            store,
            memoryService,
            new AgentSystemPromptBuilder(new FakeMemoryPolicyService())),
            chatProvider);

        var chunks = new List<AgentChatStreamChunk>();
        await foreach (var chunk in chat.ChatStreamingAsync(
            conversation.Id,
            new AgentChatRequest("Ahoj", 8, 12)))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(0, jobsDuringStream);
        Assert.Contains(chunks, chunk => chunk.Delta == "Ahoj.");
        var completed = Assert.Single(chunks, chunk => chunk.Completed is not null).Completed;
        Assert.Equal("Succeeded", completed?.Status);
        Assert.Equal("Ahoj.", completed?.AssistantMessage?.Content);
        Assert.Single(store.Jobs);
    }

    [Fact]
    public async Task Chat_saves_user_message_when_completion_fails()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);

        var memoryService = new MemoryService(
            store,
            new FakeEmbeddingProvider { IsAvailable = false },
            Options.Create(new MemoryAiOptions { SearchLimit = 10, CoreMemoryLimit = 12 }));
        var memory = new AgentMemoryService(
            store,
            memoryService,
            new AgentSystemPromptBuilder(new FakeMemoryPolicyService()));
        var chatProvider = new FakeChatCompletionProvider
        {
            Exception = new InvalidOperationException("Ollama timed out."),
            BeforeComplete = () => Assert.Empty(store.Jobs)
        };
        var conversations = new ConversationService(store);
        var chat = AgentChatHarness.Create(conversations, memory, chatProvider);

        var response = await chat.ChatAsync(conversation.Id, new AgentChatRequest("Odpověz mi.", 8, 12));

        Assert.Equal("Failed", response.Status);
        Assert.Equal("Fake", response.Provider);
        Assert.Equal("fake-chat", response.Model);
        Assert.Equal(MessageRole.User, response.UserMessage.Role);
        Assert.Null(response.AssistantMessage);
        Assert.Contains("Ollama timed out.", response.ErrorMessage, StringComparison.Ordinal);
        Assert.Single(store.Jobs);

        var messages = await store.GetRecentMessagesAsync(conversation.Id, 10);
        var message = Assert.Single(messages);
        Assert.Equal(MessageRole.User, message.Role);
    }

    [Fact]
    public async Task Chat_passes_requested_chat_model_to_the_provider()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);

        var memoryService = new MemoryService(
            store,
            new FakeEmbeddingProvider { IsAvailable = false },
            Options.Create(new MemoryAiOptions { SearchLimit = 10, CoreMemoryLimit = 12 }));
        var memory = new AgentMemoryService(
            store,
            memoryService,
            new AgentSystemPromptBuilder(new FakeMemoryPolicyService()));
        var chatProvider = new FakeChatCompletionProvider { Response = "Ahoj." };
        var conversations = new ConversationService(store);
        var chat = AgentChatHarness.Create(conversations, memory, chatProvider);

        var response = await chat.ChatAsync(
            conversation.Id,
            new AgentChatRequest("Ahoj", 8, 12, ChatModel: "gpt-oss:20b"));

        Assert.Equal("Succeeded", response.Status);
        Assert.Equal("gpt-oss:20b", response.Model);
        Assert.Equal("gpt-oss:20b", chatProvider.LastRequest?.Model);
    }

    [Fact]
    public async Task PrepareTurn_puts_pinned_identity_into_stable_prompt()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);
        store.AddMessage(new Message(conversation.Id, MessageRole.User, "What should we cook?", 1));

        var pinned = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User name is Matej.",
            MemoryType.Fact,
            0.95m,
            0.95m);
        pinned.Pin();
        store.AddMemory(pinned);

        var memoryService = new MemoryService(
            store,
            new FakeEmbeddingProvider { IsAvailable = false },
            Options.Create(new MemoryAiOptions { SearchLimit = 10, CoreMemoryLimit = 12 }));
        var agent = new AgentMemoryService(
            store,
            memoryService,
            new AgentSystemPromptBuilder(new FakeMemoryPolicyService()));

        var prepared = await agent.PrepareTurnAsync(
            conversation.Id,
            new PrepareAgentTurnRequest("What should we cook?", 5, 10));

        Assert.Equal("user-1", prepared.OwnerId);
        Assert.Equal(AgentSystemPromptBuilder.ContractVersion, prepared.PromptContractVersion);
        Assert.Equal(MemoryPolicyKind.Balanced, prepared.Policy);
        Assert.Contains(AgentSystemPromptBuilder.LatestUserWins, prepared.SystemPromptBlock);
        Assert.Contains(AgentSystemPromptBuilder.PinnedOutranksInferred, prepared.SystemPromptBlock);
        Assert.Contains("<stable_memory>", prepared.SystemPromptBlock);
        Assert.Contains("User name is Matej.", prepared.SystemPromptBlock);
        Assert.Contains("/pinned/explicit", prepared.SystemPromptBlock);
        Assert.Contains(prepared.CoreMemories, item =>
            item.IsPinned
            && item.SelectionKind == "Core"
            && item.SelectionReason.Contains("pinned", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(prepared.RecentMessages);
    }

    [Fact]
    public async Task PrepareTurn_explains_relevant_memory_selection()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);

        var relevant = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User prefers Czech.",
            MemoryType.Preference,
            0.7m,
            0.8m);
        relevant.SetEmbedding(await new FakeEmbeddingProvider().EmbedAsync(relevant.Content));
        store.AddMemory(relevant);

        var memoryService = new MemoryService(
            store,
            new FakeEmbeddingProvider { IsAvailable = true },
            Options.Create(new MemoryAiOptions
            {
                SearchLimit = 10,
                CoreMemoryLimit = 12,
                CoreImportanceThreshold = 0.95m,
                EmbeddingDimensions = 8
            }));
        var agent = new AgentMemoryService(
            store,
            memoryService,
            new AgentSystemPromptBuilder(new FakeMemoryPolicyService()));

        var prepared = await agent.PrepareTurnAsync(
            conversation.Id,
            new PrepareAgentTurnRequest("Piš česky.", 5, 10));

        var item = Assert.Single(prepared.RelevantMemories);
        Assert.Equal("Relevant", item.SelectionKind);
        Assert.NotNull(item.Similarity);
        Assert.Contains("semantic similarity", item.SelectionReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareTurn_without_memories_still_emits_memory_contract()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);

        var memoryService = new MemoryService(
            store,
            new FakeEmbeddingProvider { IsAvailable = false },
            Options.Create(new MemoryAiOptions { SearchLimit = 10, CoreMemoryLimit = 12 }));
        var agent = new AgentMemoryService(
            store,
            memoryService,
            new AgentSystemPromptBuilder(new FakeMemoryPolicyService()));

        var prepared = await agent.PrepareTurnAsync(
            conversation.Id,
            new PrepareAgentTurnRequest("Ahoj", 5, 10));

        Assert.Equal(AgentSystemPromptBuilder.ContractVersion, prepared.PromptContractVersion);
        Assert.Contains(AgentSystemPromptBuilder.NoMemoriesListed, prepared.SystemPromptBlock);
        Assert.Contains(AgentSystemPromptBuilder.DoNotClaimWrite, prepared.SystemPromptBlock);
        Assert.Empty(prepared.CoreMemories);
        Assert.Empty(prepared.RelevantMemories);
    }
}
