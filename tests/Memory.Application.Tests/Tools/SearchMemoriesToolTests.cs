namespace Memory.Application.Tests.Tools;

using System.Text.Json;
using Microsoft.Extensions.Options;
using Memory.Application.Configuration;
using Memory.Application.Memories;
using Memory.Application.Tools;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Conversations;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

public sealed class SearchMemoriesToolTests
{
    [Fact]
    public async Task Search_returns_owner_memories_and_hides_foreign_ones()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        var other = new Conversation("someone-else", "chat-2");
        store.AddConversation(conversation);
        store.AddConversation(other);

        var home = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User lives in Brno.",
            MemoryType.Fact,
            0.95m,
            0.95m);
        home.Pin();
        var foreign = new MemoryEntity(
            other.OwnerId,
            other.Id,
            MemoryScope.User,
            "User lives in Praha.",
            MemoryType.Fact,
            1.0m,
            1.0m);
        foreign.Pin();
        store.AddMemory(home);
        store.AddMemory(foreign);

        var tool = CreateTool(store);
        var result = await tool.InvokeAsync(
            new ToolCall("call_1", "search_memories", """{"query":"where do I live","limit":5}"""),
            new ToolContext(conversation.Id, conversation.OwnerId, MemoryPolicyKind.Balanced));

        Assert.True(result.Ok);
        using var document = JsonDocument.Parse(result.Content);
        Assert.Equal("where do I live", document.RootElement.GetProperty("query").GetString());
        Assert.Equal("Balanced", document.RootElement.GetProperty("policy").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("count").GetInt32());
        var hit = Assert.Single(document.RootElement.GetProperty("memories").EnumerateArray());
        Assert.Equal(home.Id, hit.GetProperty("id").GetGuid());
        Assert.Equal("Fact", hit.GetProperty("type").GetString());
        Assert.Equal("User lives in Brno.", hit.GetProperty("content").GetString());
        Assert.True(hit.GetProperty("score").GetDouble() > 0);
    }

    [Fact]
    public async Task Search_includes_user_scope_from_another_conversation_of_the_same_owner()
    {
        var store = new FakeMemoryStore();
        var current = new Conversation("user-1", "chat-1");
        var previous = new Conversation("user-1", "chat-0");
        store.AddConversation(current);
        store.AddConversation(previous);

        var memory = new MemoryEntity(
            current.OwnerId,
            previous.Id,
            MemoryScope.User,
            "User prefers dark mode.",
            MemoryType.Preference,
            0.95m,
            0.95m);
        memory.Pin();
        store.AddMemory(memory);

        var tool = CreateTool(store);
        var result = await tool.InvokeAsync(
            new ToolCall("call_1", "search_memories", """{"query":"theme"}"""),
            new ToolContext(current.Id, current.OwnerId, MemoryPolicyKind.Balanced));

        Assert.True(result.Ok);
        using var document = JsonDocument.Parse(result.Content);
        var hit = Assert.Single(document.RootElement.GetProperty("memories").EnumerateArray());
        Assert.Equal("User prefers dark mode.", hit.GetProperty("content").GetString());
    }

    [Fact]
    public async Task Search_rejects_missing_query_and_missing_context()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);
        var tool = CreateTool(store);

        var missingQuery = await tool.InvokeAsync(
            new ToolCall("call_1", "search_memories", "{}"),
            new ToolContext(conversation.Id, conversation.OwnerId, MemoryPolicyKind.Balanced));
        var missingContext = await tool.InvokeAsync(
            new ToolCall("call_2", "search_memories", """{"query":"name"}"""),
            ToolContext.None);

        Assert.False(missingQuery.Ok);
        Assert.Equal("query is required.", missingQuery.Content);
        Assert.False(missingContext.Ok);
        Assert.Equal("Tool context is required.", missingContext.Content);
    }

    [Fact]
    public async Task Search_rejects_owner_mismatch()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);
        var tool = CreateTool(store);

        var result = await tool.InvokeAsync(
            new ToolCall("call_1", "search_memories", """{"query":"name"}"""),
            new ToolContext(conversation.Id, "someone-else", MemoryPolicyKind.Balanced));

        Assert.False(result.Ok);
        Assert.Equal("Tool context does not match the conversation owner.", result.Content);
    }

    [Fact]
    public async Task Search_caps_limit_at_eight()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        store.AddConversation(conversation);
        for (var index = 0; index < 12; index++)
        {
            var memory = new MemoryEntity(
                conversation.OwnerId,
                conversation.Id,
                MemoryScope.User,
                $"User fact {index}.",
                MemoryType.Fact,
                0.95m,
                0.95m);
            memory.Pin();
            store.AddMemory(memory);
        }

        var tool = CreateTool(store);
        var result = await tool.InvokeAsync(
            new ToolCall("call_1", "search_memories", """{"query":"fact","limit":40}"""),
            new ToolContext(conversation.Id, conversation.OwnerId, MemoryPolicyKind.Balanced));

        Assert.True(result.Ok);
        using var document = JsonDocument.Parse(result.Content);
        Assert.Equal(8, document.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(8, document.RootElement.GetProperty("memories").GetArrayLength());
    }

    private static SearchMemoriesTool CreateTool(FakeMemoryStore store)
    {
        var memoryService = new MemoryService(
            store,
            new FakeEmbeddingProvider { IsAvailable = false },
            Options.Create(new MemoryAiOptions
            {
                EmbeddingDimensions = 8,
                SearchLimit = 10,
                CoreMemoryLimit = 12
            }));
        return new SearchMemoriesTool(store, memoryService);
    }
}
