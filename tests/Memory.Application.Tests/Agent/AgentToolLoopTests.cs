namespace Memory.Application.Tests.Agent;

using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.AI;
using Memory.Application.Agent;
using Memory.Application.Configuration;
using Memory.Application.Conversations;
using Memory.Application.Memories;
using Memory.Application.Tools;
using Memory.Application.Tests.Fakes;
using Memory.Application.ToolsGateway.Web;
using Memory.Domain.Conversations;
using Memory.Domain.Memories;
using Memory.Domain.Tools;
using MemoryEntity = Memory.Domain.Memories.Memory;

public sealed class AgentToolLoopTests
{
    [Fact]
    public async Task Chat_runs_get_time_then_returns_final_answer()
    {
        var utc = new DateTimeOffset(2026, 9, 10, 18, 45, 0, TimeSpan.Zero);
        var (store, conversation, memory, conversations) = CreateTurn();
        var chatProvider = new FakeChatCompletionProvider();
        chatProvider.Completions.Enqueue(new ChatCompletionResponse(
            string.Empty,
            [new ToolCall("call_1", "get_time", "{}")]));
        chatProvider.Completions.Enqueue(new ChatCompletionResponse("Je čtvrtek 10. září 2026."));
        var chat = AgentChatHarness.Create(
            conversations,
            memory,
            chatProvider,
            new FixedTimeProvider(utc),
            store: store);

        var response = await chat.ChatAsync(
            conversation.Id,
            new AgentChatRequest("Kolik je hodin?", 8, 12));

        Assert.Equal("Succeeded", response.Status);
        Assert.Equal(ToolPermissionProfile.Safe, response.PermissionProfile);
        Assert.Equal("Je čtvrtek 10. září 2026.", response.AssistantMessage?.Content);
        var trace = Assert.Single(response.ToolTrace);
        Assert.Equal("get_time", trace.Name);
        Assert.True(trace.Ok);
        Assert.Contains("2026-09-10T18:45:00", trace.Result, StringComparison.Ordinal);
        Assert.Equal(2, chatProvider.Requests.Count);
        Assert.Contains(
            chatProvider.Requests[0].Tools ?? [],
            tool => tool.Name == "get_time");
        Assert.Contains(
            chatProvider.Requests[1].Messages,
            message => message.Role == "tool"
                && message.ToolCallId == "call_1"
                && message.Content.Contains("2026-09-10T18:45:00", StringComparison.Ordinal));

        var persisted = await store.GetRecentMessagesAsync(conversation.Id, 10);
        Assert.Equal(2, persisted.Count);
        Assert.Equal(MessageRole.User, persisted[0].Role);
        Assert.Equal(MessageRole.Assistant, persisted[1].Role);
        Assert.DoesNotContain(persisted, message => message.Role == MessageRole.Tool);

        var audit = Assert.Single(store.ToolAuditLogs);
        Assert.Equal("get_time", audit.Name);
        Assert.Equal("Builtin", audit.Trust);
        Assert.Equal(["Clock"], audit.CapabilityNames);
        Assert.True(audit.Ok);
        Assert.True(audit.Invoked);
        Assert.Equal(ToolAuditOutcome.Succeeded, audit.Outcome);
        Assert.Contains("2026-09-10T18:45:00", audit.Result, StringComparison.Ordinal);
        Assert.Null(audit.Error);
        Assert.Equal(conversation.Id, audit.ConversationId);
        Assert.Equal(utc, audit.OccurredAt);
    }

    [Fact]
    public async Task Chat_runs_search_memories_then_uses_the_result()
    {
        var (store, conversation, memory, conversations) = CreateTurn();
        var home = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User lives in Brno.",
            MemoryType.Fact,
            0.95m,
            0.95m);
        home.Pin();
        store.AddMemory(home);

        var memoryService = new MemoryService(
            store,
            new FakeEmbeddingProvider { IsAvailable = false },
            Options.Create(new MemoryAiOptions { SearchLimit = 10, CoreMemoryLimit = 12 }));
        var chatProvider = new FakeChatCompletionProvider();
        chatProvider.Completions.Enqueue(new ChatCompletionResponse(
            string.Empty,
            [new ToolCall("call_1", "search_memories", """{"query":"where do I live"}""")]));
        chatProvider.Completions.Enqueue(new ChatCompletionResponse("Bydlíš v Brně."));
        var chat = AgentChatHarness.Create(
            conversations,
            memory,
            chatProvider,
            tools: [new SearchMemoriesTool(store, memoryService)],
            store: store);

        var response = await chat.ChatAsync(
            conversation.Id,
            new AgentChatRequest("Kde bydlím?", 8, 12));

        Assert.Equal("Succeeded", response.Status);
        Assert.Equal("Bydlíš v Brně.", response.AssistantMessage?.Content);
        var trace = Assert.Single(response.ToolTrace);
        Assert.Equal("search_memories", trace.Name);
        Assert.True(trace.Ok);
        Assert.Contains("User lives in Brno.", trace.Result, StringComparison.Ordinal);
        Assert.Contains(
            chatProvider.Requests[0].Messages,
            message => message.Role == "system"
                && message.Content.Contains(AgentSystemPromptBuilder.ToolMemoriesAreEvidence, StringComparison.Ordinal)
                && message.Content.Contains(AgentSystemPromptBuilder.NoUnlistedFacts, StringComparison.Ordinal));
        Assert.Contains(
            chatProvider.Requests[1].Messages,
            message => message.Role == "tool"
                && message.ToolCallId == "call_1"
                && message.Content.Contains("User lives in Brno.", StringComparison.Ordinal));

        var audit = Assert.Single(store.ToolAuditLogs);
        Assert.Equal("search_memories", audit.Name);
        Assert.Equal("Builtin", audit.Trust);
        Assert.Equal(["MemoryRead"], audit.CapabilityNames);
        Assert.True(audit.Ok);
        Assert.Equal(ToolAuditOutcome.Succeeded, audit.Outcome);
        Assert.Contains("User lives in Brno.", audit.Result, StringComparison.Ordinal);
        Assert.DoesNotContain(await store.GetRecentMessagesAsync(conversation.Id, 10), message => message.Role == MessageRole.Tool);
    }

    [Fact]
    public async Task Chat_advertises_web_search_on_the_safe_profile()
    {
        var (_, conversation, memory, conversations) = CreateTurn();
        var chatProvider = new FakeChatCompletionProvider { Response = "Ahoj." };
        var chat = AgentChatHarness.Create(
            conversations,
            memory,
            chatProvider,
            tools:
            [
                new GetTimeTool(TimeProvider.System),
                new WebSearchAgentTool(new FakeWebSearchProvider()),
                new WebFetchAgentTool(new FakeWebContentFetcher())
            ]);

        var response = await chat.ChatAsync(
            conversation.Id,
            new AgentChatRequest("Ahoj", 8, 12));

        Assert.Equal("Succeeded", response.Status);
        Assert.Equal(ToolPermissionProfile.Safe, response.PermissionProfile);
        Assert.Contains(chatProvider.LastRequest?.Tools ?? [], tool => tool.Name == WebSearchAgentTool.ToolName);
        Assert.Contains(chatProvider.LastRequest?.Tools ?? [], tool => tool.Name == WebFetchAgentTool.ToolName);
        Assert.Contains(
            chatProvider.LastRequest?.Messages ?? [],
            message => message.Role == "system"
                && message.Content.Contains("call web_search before answering", StringComparison.Ordinal));
        Assert.Empty(response.ToolTrace);
    }

    [Fact]
    public async Task Chat_falls_back_to_web_search_when_the_model_skips_tools()
    {
        var (store, conversation, memory, conversations) = CreateTurn();
        var provider = new FakeWebSearchProvider
        {
            Result = new WebSearchResult(
                "Teuta Ganna",
                "SearXNG",
                [new WebSearchHit("Teuta Ganna", "https://example.com/teuta", "Public profile", "duckduckgo", 1)])
        };
        var chatProvider = new FakeChatCompletionProvider();
        chatProvider.Completions.Enqueue(new ChatCompletionResponse(
            "Omlouvám se, ale v dostupných zdrojích se nepodařilo nic najít."));
        chatProvider.Completions.Enqueue(new ChatCompletionResponse(
            "Teuta Ganna je podle webu veřejná osoba: https://example.com/teuta"));
        var chat = AgentChatHarness.Create(
            conversations,
            memory,
            chatProvider,
            tools: [new WebSearchAgentTool(provider)],
            store: store);

        var response = await chat.ChatAsync(
            conversation.Id,
            new AgentChatRequest("Najdi mi informace o Teuta Ganna", 8, 12));

        Assert.Equal("Succeeded", response.Status);
        Assert.Equal(
            "Teuta Ganna je podle webu veřejná osoba: https://example.com/teuta",
            response.AssistantMessage?.Content);
        var trace = Assert.Single(response.ToolTrace);
        Assert.Equal(WebSearchAgentTool.ToolName, trace.Name);
        Assert.True(trace.Ok);
        Assert.Equal("Teuta Ganna", provider.LastQuery);
        Assert.Equal(2, chatProvider.Requests.Count);
        Assert.Contains(
            chatProvider.Requests[1].Messages,
            message => message.Role == "tool"
                && message.ToolCallId == WebSearchFallback.CallId
                && message.Content.Contains("https://example.com/teuta", StringComparison.Ordinal));
        Assert.DoesNotContain(
            chatProvider.Requests[1].Messages,
            message => message.Role == "assistant"
                && message.Content.Contains("nepodařilo", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Chat_does_not_advertise_or_run_untrusted_tools()
    {
        var (store, conversation, memory, conversations) = CreateTurn();
        var untrusted = new FakeTool(
            new ToolDefinition(
                "web_search",
                "Search the web.",
                [],
                ToolTrust.Untrusted,
                [ToolCapability.Network]));
        var chatProvider = new FakeChatCompletionProvider();
        chatProvider.Completions.Enqueue(new ChatCompletionResponse(
            string.Empty,
            [new ToolCall("call_1", "web_search", "{}")]));
        chatProvider.Completions.Enqueue(new ChatCompletionResponse("Bez webu."));
        var chat = AgentChatHarness.Create(
            conversations,
            memory,
            chatProvider,
            tools:
            [
                new GetTimeTool(TimeProvider.System),
                untrusted
            ],
            store: store);

        var response = await chat.ChatAsync(
            conversation.Id,
            new AgentChatRequest("Hledej to na webu.", 8, 12));

        Assert.Equal("Succeeded", response.Status);
        Assert.Equal("Bez webu.", response.AssistantMessage?.Content);
        Assert.Contains(chatProvider.Requests[0].Tools ?? [], tool => tool.Name == "get_time");
        Assert.DoesNotContain(chatProvider.Requests[0].Tools ?? [], tool => tool.Name == "web_search");
        Assert.False(untrusted.Invoked);
        var trace = Assert.Single(response.ToolTrace);
        Assert.False(trace.Ok);
        Assert.Equal("Tool 'web_search' is not allowed: trust Untrusted.", trace.Result);

        var audit = Assert.Single(store.ToolAuditLogs);
        Assert.Equal("web_search", audit.Name);
        Assert.Equal("Untrusted", audit.Trust);
        Assert.Equal(["Network"], audit.CapabilityNames);
        Assert.False(audit.Invoked);
        Assert.Equal(ToolAuditOutcome.Denied, audit.Outcome);
        Assert.Equal("Tool 'web_search' is not allowed: trust Untrusted.", audit.Error);
    }

    [Fact]
    public async Task Chat_recovers_when_the_model_calls_an_unknown_tool()
    {
        var (store, conversation, memory, conversations) = CreateTurn();
        var chatProvider = new FakeChatCompletionProvider();
        chatProvider.Completions.Enqueue(new ChatCompletionResponse(
            string.Empty,
            [new ToolCall("call_bad", "explode", "{}")]));
        chatProvider.Completions.Enqueue(new ChatCompletionResponse("Ten nástroj nemám."));
        var chat = AgentChatHarness.Create(conversations, memory, chatProvider, store: store);

        var response = await chat.ChatAsync(
            conversation.Id,
            new AgentChatRequest("Zkus explode.", 8, 12));

        Assert.Equal("Succeeded", response.Status);
        Assert.Equal("Ten nástroj nemám.", response.AssistantMessage?.Content);
        var trace = Assert.Single(response.ToolTrace);
        Assert.False(trace.Ok);
        Assert.Equal("Unknown tool 'explode'.", trace.Result);
        Assert.Contains(
            chatProvider.Requests[1].Messages,
            message => message.Role == "tool" && message.Content == "Unknown tool 'explode'.");
        Assert.Single(store.Jobs);

        var audit = Assert.Single(store.ToolAuditLogs);
        Assert.Equal("explode", audit.Name);
        Assert.Null(audit.Trust);
        Assert.False(audit.Invoked);
        Assert.Equal(ToolAuditOutcome.Unknown, audit.Outcome);
        Assert.Equal("Unknown tool 'explode'.", audit.Error);
    }

    [Fact]
    public async Task Chat_without_registered_tools_skips_the_tool_loop()
    {
        var (_, conversation, memory, conversations) = CreateTurn();
        var chatProvider = new FakeChatCompletionProvider { Response = "Ahoj." };
        var chat = AgentChatHarness.Create(
            conversations,
            memory,
            chatProvider,
            tools: []);

        var response = await chat.ChatAsync(
            conversation.Id,
            new AgentChatRequest("Ahoj", 8, 12));

        Assert.Equal("Succeeded", response.Status);
        Assert.Equal("Ahoj.", response.AssistantMessage?.Content);
        Assert.Empty(response.ToolTrace);
        Assert.Single(chatProvider.Requests);
        Assert.Null(chatProvider.LastRequest?.Tools);
        Assert.DoesNotContain(
            chatProvider.LastRequest?.Messages ?? [],
            message => message.Content.Contains("Available:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Chat_does_not_advertise_external_http_tools_on_the_default_turn()
    {
        var (_, conversation, memory, conversations) = CreateTurn();
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
        var chat = AgentChatHarness.Create(
            conversations,
            memory,
            chatProvider,
            tools:
            [
                new GetTimeTool(TimeProvider.System),
                httpTool
            ]);

        var response = await chat.ChatAsync(
            conversation.Id,
            new AgentChatRequest("Ahoj", 8, 12));

        Assert.Equal("Succeeded", response.Status);
        Assert.Contains(chatProvider.LastRequest?.Tools ?? [], tool => tool.Name == "get_time");
        Assert.DoesNotContain(chatProvider.LastRequest?.Tools ?? [], tool => tool.Name == "echo_http");
        Assert.False(handler.Invoked);
        Assert.Empty(response.ToolTrace);
    }

    [Fact]
    public async Task Chat_advertises_http_tool_when_the_turn_allows_untrusted_network()
    {
        var (store, conversation, memory, conversations) = CreateTurn();
        var handler = new StubHttpHandler { ResponseBody = """{"pong":true}""" };
        var httpTool = HttpTool.Create(
            new ExternalToolDefinition
            {
                Name = "echo_http",
                Description = "Echo",
                Url = "https://example.com/tools/echo"
            },
            new HttpClient(handler, disposeHandler: false));
        var chatProvider = new FakeChatCompletionProvider();
        chatProvider.Completions.Enqueue(new ChatCompletionResponse(
            string.Empty,
            [new ToolCall("call_1", "echo_http", """{"text":"hi"}""")]));
        chatProvider.Completions.Enqueue(new ChatCompletionResponse("Plugin odpověděl."));
        var chat = AgentChatHarness.Create(
            conversations,
            memory,
            chatProvider,
            tools:
            [
                new GetTimeTool(TimeProvider.System),
                httpTool
            ],
            store: store);

        var response = await chat.ChatAsync(
            conversation.Id,
            new AgentChatRequest(
                "Zavolej echo.",
                8,
                12,
                ToolPermissionProfile.NetworkOnce));

        Assert.Equal("Succeeded", response.Status);
        Assert.Equal(ToolPermissionProfile.NetworkOnce, response.PermissionProfile);
        Assert.Equal("Plugin odpověděl.", response.AssistantMessage?.Content);
        Assert.Contains(chatProvider.Requests[0].Tools ?? [], tool => tool.Name == "get_time");
        Assert.Contains(chatProvider.Requests[0].Tools ?? [], tool => tool.Name == "echo_http");
        Assert.True(handler.Invoked);
        var trace = Assert.Single(response.ToolTrace);
        Assert.True(trace.Ok);
        Assert.Contains("pong", trace.Result, StringComparison.Ordinal);

        var audit = Assert.Single(store.ToolAuditLogs);
        Assert.Equal("echo_http", audit.Name);
        Assert.Equal("Untrusted", audit.Trust);
        Assert.Equal(["Network"], audit.CapabilityNames);
        Assert.True(audit.Invoked);
        Assert.True(audit.Ok);
        Assert.Equal(ToolAuditOutcome.Succeeded, audit.Outcome);
        Assert.Contains("pong", audit.Result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NetworkOnce_does_not_advertise_http_after_the_budget_is_used()
    {
        var (store, conversation, memory, conversations) = CreateTurn();
        var handler = new StubHttpHandler { ResponseBody = "ok" };
        var httpTool = HttpTool.Create(
            new ExternalToolDefinition
            {
                Name = "echo_http",
                Description = "Echo",
                Url = "https://example.com/tools/echo"
            },
            new HttpClient(handler, disposeHandler: false));
        var chatProvider = new FakeChatCompletionProvider();
        chatProvider.Completions.Enqueue(new ChatCompletionResponse(
            string.Empty,
            [new ToolCall("call_1", "echo_http", "{}")]));
        chatProvider.Completions.Enqueue(new ChatCompletionResponse(
            string.Empty,
            [new ToolCall("call_2", "echo_http", "{}")]));
        chatProvider.Completions.Enqueue(new ChatCompletionResponse("Hotovo."));
        var chat = AgentChatHarness.Create(
            conversations,
            memory,
            chatProvider,
            tools:
            [
                new GetTimeTool(TimeProvider.System),
                httpTool
            ],
            store: store);

        var response = await chat.ChatAsync(
            conversation.Id,
            new AgentChatRequest("Zavolej echo dvakrát.", 8, 12, ToolPermissionProfile.NetworkOnce));

        Assert.Equal("Succeeded", response.Status);
        Assert.Equal("Hotovo.", response.AssistantMessage?.Content);
        Assert.Contains(chatProvider.Requests[0].Tools ?? [], tool => tool.Name == "echo_http");
        Assert.DoesNotContain(chatProvider.Requests[1].Tools ?? [], tool => tool.Name == "echo_http");
        Assert.Equal(1, handler.InvokeCount);
        Assert.Equal(2, response.ToolTrace.Count);
        Assert.True(response.ToolTrace[0].Ok);
        Assert.False(response.ToolTrace[1].Ok);
        Assert.Contains("NetworkOnce already used", response.ToolTrace[1].Result, StringComparison.Ordinal);
        Assert.Equal(2, store.ToolAuditLogs.Count);
        Assert.Equal(ToolAuditOutcome.Succeeded, store.ToolAuditLogs[0].Outcome);
        Assert.Equal(ToolAuditOutcome.Denied, store.ToolAuditLogs[1].Outcome);
    }

    [Fact]
    public async Task Chat_rejects_admin_profile_before_saving_messages()
    {
        var (store, conversation, memory, conversations) = CreateTurn();
        var chat = AgentChatHarness.Create(conversations, memory, new FakeChatCompletionProvider { Response = "Ahoj." });

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            chat.ChatAsync(
                conversation.Id,
                new AgentChatRequest("Ahoj", 8, 12, ToolPermissionProfile.Admin)));

        Assert.Contains("Admin", exception.Message, StringComparison.Ordinal);
        Assert.Empty(await store.GetRecentMessagesAsync(conversation.Id, 10));
        Assert.Empty(store.ToolAuditLogs);
    }

    [Fact]
    public async Task Chat_fails_when_tool_loop_exceeds_max_rounds()
    {
        var (_, conversation, memory, conversations) = CreateTurn();
        var chatProvider = new FakeChatCompletionProvider();
        for (var round = 0; round < AgentChatService.MaxToolRounds; round++)
        {
            chatProvider.Completions.Enqueue(new ChatCompletionResponse(
                string.Empty,
                [new ToolCall($"call_{round}", "get_time", "{}")]));
        }

        var chat = AgentChatHarness.Create(conversations, memory, chatProvider);
        var response = await chat.ChatAsync(
            conversation.Id,
            new AgentChatRequest("Kolik je hodin?", 8, 12));

        Assert.Equal("Failed", response.Status);
        Assert.Null(response.AssistantMessage);
        Assert.Equal("Tool loop exceeded the maximum number of rounds.", response.ErrorMessage);
        Assert.Equal(AgentChatService.MaxToolRounds, response.ToolTrace.Count);
        Assert.Equal(AgentChatService.MaxToolRounds, chatProvider.Requests.Count);
    }

    private static (
        FakeMemoryStore Store,
        Conversation Conversation,
        AgentMemoryService Memory,
        ConversationService Conversations) CreateTurn()
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
        return (store, conversation, memory, new ConversationService(store));
    }
}
