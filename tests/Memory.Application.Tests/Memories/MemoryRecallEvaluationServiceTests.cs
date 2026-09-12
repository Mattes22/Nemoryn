namespace Memory.Application.Tests.Memories;

using Microsoft.Extensions.Options;
using Memory.Application.Configuration;
using Memory.Application.Exceptions;
using Memory.Application.Memories;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Conversations;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

public sealed class MemoryRecallEvaluationServiceTests
{
    [Fact]
    public async Task Evaluate_hits_core_identity_memories_for_profile_questions()
    {
        var store = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingProvider();
        var conversation = new Conversation("matej", "chat-1");
        store.AddConversation(conversation);

        var name = await AddMemoryAsync(
            store,
            embeddings,
            conversation,
            "User's name is Matej.",
            MemoryType.Fact,
            0.95m,
            pin: true);
        var home = await AddMemoryAsync(
            store,
            embeddings,
            conversation,
            "User lives in Brno.",
            MemoryType.Fact,
            0.9m);
        var style = await AddMemoryAsync(
            store,
            embeddings,
            conversation,
            "User prefers concise Czech answers.",
            MemoryType.Preference,
            0.9m);
        await AddMemoryAsync(
            store,
            embeddings,
            conversation,
            "The current sprint uses Jira.",
            MemoryType.Fact,
            0.4m,
            MemoryScope.Conversation);

        var result = await CreateService(store, embeddings).EvaluateAsync(
            conversation.Id,
            new EvaluateMemoryRecallRequest(
                [
                    new MemoryRecallCase("name", "Jak se jmenuju?", [name.Id], null),
                    new MemoryRecallCase("home", "Kde bydlím?", [home.Id], null),
                    new MemoryRecallCase("style", "Jak mám rád odpovědi?", [style.Id], null)
                ],
                Limit: 8));

        Assert.Equal(3, result.Summary.CaseCount);
        Assert.Equal(3, result.Summary.EvaluableCaseCount);
        Assert.Equal(3, result.Summary.Hits);
        Assert.Equal(0, result.Summary.Misses);
        Assert.Equal(1d, result.Summary.HitRate);
        Assert.Equal(conversation.OwnerId, result.OwnerId);
        Assert.True(result.Thresholds.EmbeddingAvailable);
        Assert.True(result.Summary.MeanReciprocalRank > 0);

        Assert.All(result.Cases, recallCase =>
        {
            Assert.True(recallCase.Hit);
            Assert.NotNull(recallCase.BestExpectedRank);
            Assert.True(recallCase.ReciprocalRank > 0);
            var expected = Assert.Single(recallCase.Expectations);
            Assert.True(expected.Found);
            Assert.Equal("Core", expected.SelectionKind);
            Assert.Contains(
                recallCase.Selected,
                item => item.Memory.Id == expected.MemoryId
                    && item.Memory.SelectionReason.Contains("Core memory", StringComparison.OrdinalIgnoreCase));
        });

        Assert.Equal(1, result.Cases[0].BestExpectedRank);

        Assert.Contains(result.Cases[0].Selected, item =>
            item.Memory.Id == name.Id && item.ScoreBreakdown.Score > 0);
    }

    [Fact]
    public async Task Evaluate_reports_miss_reason_when_expected_memory_is_not_retrieved()
    {
        var store = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingProvider();
        var conversation = new Conversation("matej", "chat-1");
        store.AddConversation(conversation);

        var name = await AddMemoryAsync(
            store,
            embeddings,
            conversation,
            "User's name is Matej.",
            MemoryType.Fact,
            0.95m,
            pin: true);
        var noise = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.Conversation,
            "The kettle boils at 100 degrees.",
            MemoryType.Summary,
            0.2m,
            0.2m);
        noise.SetEmbedding(await embeddings.EmbedAsync("qqqqqqqq unrelated vector seed"));
        store.AddMemory(noise);

        var result = await CreateService(store, embeddings).EvaluateAsync(
            conversation.Id,
            new EvaluateMemoryRecallRequest(
                [
                    new MemoryRecallCase("name", "Jak se jmenuju?", [name.Id], null),
                    new MemoryRecallCase("noise", "Jak se jmenuju?", [noise.Id], null)
                ],
                Limit: 8));

        Assert.Equal(2, result.Summary.EvaluableCaseCount);
        Assert.Equal(1, result.Summary.Hits);
        Assert.Equal(1, result.Summary.Misses);
        Assert.Equal(0.5d, result.Summary.HitRate);

        Assert.True(result.Cases[0].Hit);
        Assert.False(result.Cases[1].Hit);
        Assert.Equal(0d, result.Cases[1].ReciprocalRank);
        var miss = Assert.Single(result.Cases[1].Expectations);
        Assert.False(miss.Found);
        Assert.NotNull(miss.MissReason);
        Assert.NotNull(miss.ScoreBreakdown);
        Assert.DoesNotContain(result.Cases[1].Selected, item => item.Memory.Id == noise.Id);
    }

    [Fact]
    public async Task Evaluate_matches_expected_content_without_memory_id()
    {
        var store = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingProvider();
        var conversation = new Conversation("matej", "chat-1");
        store.AddConversation(conversation);
        await AddMemoryAsync(
            store,
            embeddings,
            conversation,
            "User's name is Matej.",
            MemoryType.Fact,
            0.95m,
            pin: true);

        var result = await CreateService(store, embeddings).EvaluateAsync(
            conversation.Id,
            new EvaluateMemoryRecallRequest(
                [new MemoryRecallCase("name", "Jak se jmenuju?", null, ["Matej"])],
                Limit: 8));

        var recallCase = Assert.Single(result.Cases);
        Assert.True(recallCase.Hit);
        var expectation = Assert.Single(recallCase.Expectations);
        Assert.Equal("Matej", expectation.ExpectedContentContains);
        Assert.Contains("Matej", expectation.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluate_requires_cases_and_known_conversation()
    {
        var store = new FakeMemoryStore();
        var service = CreateService(store, new FakeEmbeddingProvider());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EvaluateAsync(Guid.NewGuid(), new EvaluateMemoryRecallRequest([], 8)));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.EvaluateAsync(
                Guid.NewGuid(),
                new EvaluateMemoryRecallRequest(
                    [new MemoryRecallCase("name", "Jak se jmenuju?", null, ["Matej"])],
                    8)));
    }

    private static async Task<MemoryEntity> AddMemoryAsync(
        FakeMemoryStore store,
        FakeEmbeddingProvider embeddings,
        Conversation conversation,
        string content,
        MemoryType type,
        decimal importance,
        MemoryScope scope = MemoryScope.User,
        bool pin = false)
    {
        var memory = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            scope,
            content,
            type,
            importance,
            0.95m,
            isPinned: pin);
        memory.SetEmbedding(await embeddings.EmbedAsync(memory.Content));
        store.AddMemory(memory);
        return memory;
    }

    private static MemoryRecallEvaluationService CreateService(
        FakeMemoryStore store,
        FakeEmbeddingProvider embeddings)
    {
        var options = Options.Create(new MemoryAiOptions
        {
            EmbeddingDimensions = 8,
            SearchLimit = 10,
            MinRelevantSimilarity = 0.62d,
            MinRelevantScore = 0.70d,
            MaxRelevantMemories = 8,
            CoreMemoryLimit = 12,
            CoreImportanceThreshold = 0.75m
        });
        var memoryService = new MemoryService(store, embeddings, options);
        return new MemoryRecallEvaluationService(store, memoryService, embeddings, options);
    }
}
