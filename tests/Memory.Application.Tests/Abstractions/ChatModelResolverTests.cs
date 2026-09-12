namespace Memory.Application.Tests.Abstractions;

using Memory.Application.Abstractions.AI;
using Memory.Application.Configuration;
using Memory.Application.Tests.Fakes;

public sealed class ChatModelResolverTests
{
    [Fact]
    public async Task List_excludes_embedding_and_always_includes_chat_model()
    {
        var resolver = CreateResolver(
            ["nomic-embed-text", "llama3.1", "gpt-oss:20b"],
            chatModel: "gpt-oss:20b",
            embeddingModel: "nomic-embed-text");

        var models = await resolver.ListChatModelsAsync();

        Assert.Equal(["gpt-oss:20b", "llama3.1"], models);
    }

    [Fact]
    public async Task List_inserts_configured_chat_model_when_catalog_is_empty()
    {
        var resolver = CreateResolver([], chatModel: "gpt-oss:20b");

        var models = await resolver.ListChatModelsAsync();

        Assert.Equal(["gpt-oss:20b"], models);
    }

    [Fact]
    public async Task Resolve_empty_request_returns_configured_chat_model()
    {
        var resolver = CreateResolver(["llama3.1"], chatModel: "gpt-oss:20b");

        Assert.Equal("gpt-oss:20b", await resolver.ResolveChatModelAsync(null));
        Assert.Equal("gpt-oss:20b", await resolver.ResolveChatModelAsync(" "));
    }

    [Fact]
    public async Task Resolve_matches_latest_suffix()
    {
        var resolver = CreateResolver(["llama3.1", "gpt-oss:20b"], chatModel: "gpt-oss:20b");

        Assert.Equal("llama3.1", await resolver.ResolveChatModelAsync("llama3.1:latest"));
    }

    [Fact]
    public async Task Resolve_rejects_embedding_model()
    {
        var resolver = CreateResolver(["llama3.1"], chatModel: "gpt-oss:20b", embeddingModel: "nomic-embed-text");

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => resolver.ResolveChatModelAsync("nomic-embed-text"));

        Assert.Contains("embedding", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Resolve_rejects_unknown_model()
    {
        var resolver = CreateResolver(["llama3.1"], chatModel: "gpt-oss:20b");

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => resolver.ResolveChatModelAsync("missing-model"));

        Assert.Contains("Unknown chat model", exception.Message, StringComparison.Ordinal);
    }

    private static ChatModelResolver CreateResolver(
        IReadOnlyList<string> catalog,
        string chatModel,
        string embeddingModel = "nomic-embed-text")
    {
        return new ChatModelResolver(
            new FakeAiModelCatalog { Models = catalog },
            new FakeOptionsMonitor<MemoryAiOptions>(new MemoryAiOptions
            {
                ChatModel = chatModel,
                EmbeddingModel = embeddingModel
            }));
    }
}
