namespace Memory.Application.Tests.OpenAiCompatible;

using Microsoft.Extensions.Options;
using Memory.Application.Agent;
using Memory.Application.Tests.Agent;
using Memory.Application.Configuration;
using Memory.Application.Conversations;
using Memory.Application.Exceptions;
using Memory.Application.Memories;
using Memory.Application.OpenAiCompatible;
using Memory.Application.Tests.Fakes;
using Memory.Application.Tools;
using Memory.Domain.Conversations;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

public sealed class OpenAiCompatibleChatServiceTests
{
    [Fact]
    public async Task Complete_upserts_conversation_by_external_id_and_chats()
    {
        var store = new FakeMemoryStore();
        var service = CreateService(store, "Ahoj.");

        var result = await service.CompleteAsync(
            new OpenAiCompatibleChatCommand("matej", "openai-chat-1", "Jak se jmenuju?", 8, 12));

        Assert.Equal("matej", result.Conversation.OwnerId);
        Assert.Equal("openai-chat-1", result.Conversation.ExternalId);
        Assert.Equal("Succeeded", result.Chat.Status);
        Assert.Equal("Ahoj.", result.Chat.AssistantMessage?.Content);
        Assert.Single(store.Conversations);
        Assert.Equal(2, (await store.GetRecentMessagesAsync(result.Conversation.Id, 10)).Count);
        Assert.Single(store.Jobs);
    }

    [Fact]
    public async Task Complete_reuses_existing_conversation_guid_for_the_same_owner()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-1");
        store.AddConversation(conversation);
        var service = CreateService(store, "OK");

        var result = await service.CompleteAsync(
            new OpenAiCompatibleChatCommand("matej", conversation.Id.ToString(), "Ahoj", 8, 12));

        Assert.Equal(conversation.Id, result.Conversation.Id);
        Assert.Single(store.Conversations);
        Assert.Equal("Succeeded", result.Chat.Status);
    }

    [Fact]
    public async Task Complete_rejects_conversation_guid_owned_by_someone_else()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("other", "chat-1");
        store.AddConversation(conversation);
        var service = CreateService(store, "OK");

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CompleteAsync(
                new OpenAiCompatibleChatCommand("matej", conversation.Id.ToString(), "Ahoj", 8, 12)));

        Assert.Contains("does not belong", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(store.Jobs);
    }

    [Fact]
    public async Task Complete_includes_stable_memory_in_the_agent_turn()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-1");
        var pinned = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User name is Matej.",
            MemoryType.Fact,
            0.95m,
            0.95m);
        pinned.Pin();
        store.AddConversation(conversation);
        store.AddMemory(pinned);
        var service = CreateService(store, "Ahoj Mateji.");

        var result = await service.CompleteAsync(
            new OpenAiCompatibleChatCommand("matej", conversation.Id.ToString(), "Jak se jmenuju?", 8, 12));

        Assert.Contains("User name is Matej.", result.Chat.PreparedTurn.SystemPromptBlock);
        Assert.Equal(AgentSystemPromptBuilder.ContractVersion, result.Chat.PreparedTurn.PromptContractVersion);
        Assert.Contains(result.Chat.PreparedTurn.CoreMemories, item => item.Content == "User name is Matej.");
    }

    [Fact]
    public async Task Stream_upserts_conversation_and_yields_deltas()
    {
        var store = new FakeMemoryStore();
        var service = CreateService(store, "Ahoj.");

        var chunks = new List<AgentChatStreamChunk>();
        await foreach (var chunk in service.StreamAsync(
            new OpenAiCompatibleChatCommand("matej", "openai-chat-1", "Jak se jmenuju?", 8, 12)))
        {
            chunks.Add(chunk);
        }

        Assert.Contains(chunks, chunk => chunk.Delta == "Ahoj.");
        var completed = Assert.Single(chunks, chunk => chunk.Completed is not null).Completed;
        Assert.Equal("Succeeded", completed?.Status);
        Assert.Equal("matej", store.Conversations.Single().OwnerId);
        Assert.Equal("openai-chat-1", store.Conversations.Single().ExternalId);
        Assert.Single(store.Jobs);
    }

    [Fact]
    public async Task Complete_does_not_enable_untrusted_network_tools()
    {
        var store = new FakeMemoryStore();
        var handler = new StubHttpHandler();
        var httpTool = HttpTool.Create(
            new ExternalToolDefinition
            {
                Name = "echo_http",
                Description = "Echo",
                Url = "https://example.com/tools/echo"
            },
            new HttpClient(handler, disposeHandler: false));
        var chatProvider = new FakeChatCompletionProvider { Response = "Ahoj." };
        var service = CreateService(
            store,
            chatProvider,
            [
                new GetTimeTool(TimeProvider.System),
                httpTool
            ]);

        var result = await service.CompleteAsync(
            new OpenAiCompatibleChatCommand("matej", "openai-chat-1", "Zavolej echo.", 8, 12));

        Assert.Equal("Succeeded", result.Chat.Status);
        Assert.Contains(chatProvider.LastRequest?.Tools ?? [], tool => tool.Name == "get_time");
        Assert.DoesNotContain(chatProvider.LastRequest?.Tools ?? [], tool => tool.Name == "echo_http");
        Assert.False(handler.Invoked);
        Assert.Empty(result.Chat.ToolTrace);
        Assert.Equal(ToolPermissionProfile.Safe, result.Chat.PermissionProfile);
    }

    [Fact]
    public async Task Complete_advertises_web_search_on_the_safe_profile()
    {
        var store = new FakeMemoryStore();
        var chatProvider = new FakeChatCompletionProvider { Response = "Ahoj." };
        var service = CreateService(
            store,
            chatProvider,
            [
                new GetTimeTool(TimeProvider.System),
                new WebSearchAgentTool(new FakeWebSearchProvider())
            ]);

        var result = await service.CompleteAsync(
            new OpenAiCompatibleChatCommand("matej", "openai-chat-1", "Ahoj", 8, 12));

        Assert.Equal("Succeeded", result.Chat.Status);
        Assert.Equal(ToolPermissionProfile.Safe, result.Chat.PermissionProfile);
        Assert.Contains(chatProvider.LastRequest?.Tools ?? [], tool => tool.Name == WebSearchAgentTool.ToolName);
        Assert.Empty(result.Chat.ToolTrace);
    }

    [Fact]
    public async Task Complete_passes_chat_model_to_the_provider()
    {
        var store = new FakeMemoryStore();
        var chatProvider = new FakeChatCompletionProvider { Response = "Ahoj." };
        var service = CreateService(store, chatProvider, [new GetTimeTool(TimeProvider.System)]);

        var result = await service.CompleteAsync(
            new OpenAiCompatibleChatCommand("matej", "openai-chat-1", "Jak se jmenuju?", 8, 12, ChatModel: "llama3.1"));

        Assert.Equal("Succeeded", result.Chat.Status);
        Assert.Equal("llama3.1", result.Chat.Model);
        Assert.Equal("llama3.1", chatProvider.LastRequest?.Model);
    }

    private static OpenAiCompatibleChatService CreateService(FakeMemoryStore store, string assistantReply)
    {
        return CreateService(
            store,
            new FakeChatCompletionProvider { Response = assistantReply },
            [new GetTimeTool(TimeProvider.System)]);
    }

    private static OpenAiCompatibleChatService CreateService(
        FakeMemoryStore store,
        FakeChatCompletionProvider chatProvider,
        IEnumerable<ITool> tools)
    {
        var memoryService = new MemoryService(
            store,
            new FakeEmbeddingProvider { IsAvailable = false },
            Options.Create(new MemoryAiOptions { SearchLimit = 10, CoreMemoryLimit = 12 }));
        var conversations = new ConversationService(store);
        var chat = AgentChatHarness.Create(
            conversations,
            new AgentMemoryService(
            store,
            memoryService,
            new AgentSystemPromptBuilder(new FakeMemoryPolicyService())),
            chatProvider,
            tools: tools);

        return new OpenAiCompatibleChatService(conversations, chat);
    }
}
