namespace Memory.Application.Tests.Memories;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.AI;
using Memory.Application.Configuration;
using Memory.Application.Memories;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Conversations;
using Memory.Domain.Ingestion;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

public sealed class MemoryIngestionServiceTests
{
    [Fact]
    public async Task Ingest_supersedes_similar_user_memory_from_another_conversation()
    {
        var store = new FakeMemoryStore();
        var previous = new Conversation("user-1", "chat-0");
        var current = new Conversation("user-1", "chat-1");
        var message = new Message(current.Id, MessageRole.User, "I drink coffee now.", 1);
        store.AddConversation(previous);
        store.AddConversation(current);
        store.AddMessage(message);

        var existing = new MemoryEntity(
            previous.OwnerId,
            previous.Id,
            MemoryScope.User,
            "User likes tea.",
            MemoryType.Preference,
            0.7m,
            0.7m);
        existing.SetEmbedding(await Embed("User likes tea."));
        store.AddMemory(existing);

        var service = CreateService(
            store,
            StrongPreference("User likes coffee.", "User switched drinks."),
            new DeterministicEmbeddingProvider(),
            new MemoryAiOptions
            {
                SimilarityThreshold = 0.0f,
                RecentMessageWindow = 10,
                EmbeddingDimensions = 8
            });

        await service.IngestFromMessageAsync(current.Id, message.Id);

        var memories = store.Memories.ToArray();
        var replacement = memories.Single(memory => memory.Id != existing.Id);

        Assert.Equal(MemoryStatus.Superseded, existing.Status);
        Assert.Equal(replacement.Id, existing.SupersededByMemoryId);
        Assert.Equal(MemoryScope.User, replacement.Scope);
        Assert.Equal("User likes coffee.", replacement.Content);
        Assert.Equal(MemoryOrigin.Inferred, replacement.Origin);
    }

    [Fact]
    public async Task Ingest_skips_exact_fingerprint_duplicate()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        var message = new Message(conversation.Id, MessageRole.User, "I like tea.", 1);
        store.AddConversation(conversation);
        store.AddMessage(message);

        var existing = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User likes tea.",
            MemoryType.Preference,
            0.7m,
            0.7m);
        store.AddMemory(existing);

        var service = CreateService(
            store,
            StrongPreference("user likes tea."),
            new FakeEmbeddingProvider { IsAvailable = false });

        await service.IngestFromMessageAsync(conversation.Id, message.Id);

        Assert.Single(store.Memories);
        Assert.Empty(store.Candidates);
        Assert.Empty(store.Evidence);
    }

    [Fact]
    public async Task ProcessNext_skips_when_extractor_is_disabled()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        var message = new Message(conversation.Id, MessageRole.User, "I drink coffee now.", 1);
        var job = new MemoryIngestionJob(conversation.Id, message.Id);
        store.AddConversation(conversation);
        store.AddMessage(message);
        store.AddIngestionJob(job);

        var service = CreateService(
            store,
            new FakeMemoryExtractor { IsAvailable = false },
            new FakeEmbeddingProvider());

        var processed = await service.ProcessNextAsync();

        Assert.True(processed);
        Assert.Equal(IngestionJobStatus.Skipped, job.Status);
        Assert.Empty(store.Memories);
    }

    [Fact]
    public async Task ProcessNext_skips_when_conversation_or_message_is_missing()
    {
        var store = new FakeMemoryStore();
        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var job = new MemoryIngestionJob(conversationId, messageId);
        store.AddIngestionJob(job);

        var service = CreateService(
            store,
            StrongPreference("User likes coffee."),
            new DeterministicEmbeddingProvider());

        var processed = await service.ProcessNextAsync();

        Assert.True(processed);
        Assert.Equal(IngestionJobStatus.Skipped, job.Status);
        Assert.Contains("not found", job.LastError, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(store.Memories);
        Assert.Empty(store.Candidates);
    }

    [Fact]
    public async Task ProcessNext_fails_when_all_extracted_candidates_fail_to_persist()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        var message = new Message(conversation.Id, MessageRole.User, "I drink coffee now.", 1);
        var job = new MemoryIngestionJob(conversation.Id, message.Id);
        store.AddConversation(conversation);
        store.AddMessage(message);
        store.AddIngestionJob(job);

        var service = CreateService(
            store,
            StrongPreference("User likes coffee."),
            new ThrowingEmbeddingProvider(),
            new MemoryAiOptions { EmbeddingDimensions = 8 });

        var processed = await service.ProcessNextAsync();

        Assert.True(processed);
        Assert.Equal(IngestionJobStatus.Pending, job.Status);
        Assert.Contains("failed for all", job.LastError, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(store.Memories);
    }

    [Fact]
    public async Task Ingest_keeps_weak_candidate_pending_without_overwriting_pinned_memory()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        var message = new Message(conversation.Id, MessageRole.User, "Maybe I like coffee.", 1);
        store.AddConversation(conversation);
        store.AddMessage(message);

        var pinned = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User likes tea.",
            MemoryType.Preference,
            0.9m,
            0.95m,
            origin: MemoryOrigin.Explicit,
            isPinned: true);
        pinned.SetEmbedding(await Embed("User likes tea."));
        store.AddMemory(pinned);

        var service = CreateService(
            store,
            Preference("User likes coffee.", 0.6m, 0.6m),
            new DeterministicEmbeddingProvider(),
            new MemoryAiOptions
            {
                SimilarityThreshold = 0.0f,
                RecentMessageWindow = 10,
                EmbeddingDimensions = 8,
                MinPersistConfidence = 0.5m,
                MinPersistImportance = 0.3m
            });

        await service.IngestFromMessageAsync(conversation.Id, message.Id);

        Assert.Single(store.Memories);
        Assert.Equal(MemoryStatus.Active, pinned.Status);
        Assert.True(pinned.IsPinned);
        Assert.Single(store.Candidates);
        Assert.Equal(MemoryCandidateStatus.Pending, store.Candidates.Single().Status);
        Assert.Empty(store.Conflicts);
    }

    [Fact]
    public async Task Ingest_creates_conflict_when_strong_candidate_cannot_supersede_pinned_memory()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        var message = new Message(conversation.Id, MessageRole.User, "I like coffee now.", 1);
        store.AddConversation(conversation);
        store.AddMessage(message);

        var pinned = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User likes tea.",
            MemoryType.Preference,
            0.9m,
            0.95m,
            origin: MemoryOrigin.Explicit,
            isPinned: true);
        pinned.SetEmbedding(await Embed("User likes tea."));
        store.AddMemory(pinned);

        var service = CreateService(
            store,
            StrongPreference("User likes coffee."),
            new DeterministicEmbeddingProvider(),
            new MemoryAiOptions
            {
                SimilarityThreshold = 0.0f,
                RecentMessageWindow = 10,
                EmbeddingDimensions = 8
            });

        await service.IngestFromMessageAsync(conversation.Id, message.Id);

        Assert.Single(store.Memories);
        Assert.Equal(MemoryStatus.Active, pinned.Status);
        var candidate = Assert.Single(store.Candidates);
        Assert.Equal(MemoryCandidateStatus.Pending, candidate.Status);
        var conflict = Assert.Single(store.Conflicts);
        Assert.Equal(MemoryConflictStatus.Pending, conflict.Status);
        Assert.Equal(candidate.Id, conflict.CandidateId);
        Assert.Equal(pinned.Id, conflict.ConflictingMemoryId);
        Assert.Single(store.Evidence);
    }

    [Fact]
    public async Task Ingest_keeps_contradicting_candidate_pending_with_conflict()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        var message = new Message(conversation.Id, MessageRole.User, "I like coffee now.", 1);
        store.AddConversation(conversation);
        store.AddMessage(message);

        var existing = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User likes tea.",
            MemoryType.Preference,
            0.7m,
            0.7m);
        existing.SetEmbedding(await Embed("User likes tea."));
        store.AddMemory(existing);

        var detector = new FakeContradictionDetector
        {
            IsAvailable = true,
            Decision = new MemoryContradictionDecision(
                MemoryContradictionRelation.Contradicts,
                existing.Id,
                0.9m,
                "Tea and coffee conflict.")
        };

        var service = CreateService(
            store,
            StrongPreference("User likes coffee."),
            new DeterministicEmbeddingProvider(),
            new MemoryAiOptions
            {
                SimilarityThreshold = 0.0f,
                RecentMessageWindow = 10,
                EmbeddingDimensions = 8,
                ContradictionConfidenceThreshold = 0.72m
            },
            detector);

        await service.IngestFromMessageAsync(conversation.Id, message.Id);

        Assert.Single(store.Memories);
        Assert.Equal(MemoryStatus.Active, existing.Status);
        var candidate = Assert.Single(store.Candidates);
        Assert.Equal(MemoryCandidateStatus.Pending, candidate.Status);
        var conflict = Assert.Single(store.Conflicts);
        Assert.Equal(existing.Id, conflict.ConflictingMemoryId);
        Assert.Equal("Tea and coffee conflict.", conflict.Reason);
        Assert.Single(store.Evidence);
    }

    [Fact]
    public async Task Ingest_promotes_after_repeated_evidence()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        var firstMessage = new Message(conversation.Id, MessageRole.User, "I like espresso.", 1);
        var secondMessage = new Message(conversation.Id, MessageRole.User, "Espresso is my drink.", 2);
        store.AddConversation(conversation);
        store.AddMessage(firstMessage);
        store.AddMessage(secondMessage);

        var extractor = Preference("User likes espresso.", 0.5m, 0.6m);
        var options = new MemoryAiOptions
        {
            RecentMessageWindow = 10,
            MinPersistConfidence = 0.5m,
            MinPersistImportance = 0.3m,
            AutoPromoteConfidence = 0.95m,
            AutoPromoteImportance = 0.95m,
            AutoPromoteEvidenceCount = 2
        };
        var embeddings = new FakeEmbeddingProvider { IsAvailable = false };
        var service = CreateService(store, extractor, embeddings, options);

        await service.IngestFromMessageAsync(conversation.Id, firstMessage.Id);

        Assert.Empty(store.Memories);
        var pending = Assert.Single(store.Candidates);
        Assert.Equal(1, pending.EvidenceCount);
        Assert.Single(store.Evidence);

        await service.IngestFromMessageAsync(conversation.Id, secondMessage.Id);

        var memory = Assert.Single(store.Memories);
        Assert.Equal("User likes espresso.", memory.Content);
        Assert.Equal(MemoryCandidateStatus.Promoted, pending.Status);
        Assert.Equal(2, pending.EvidenceCount);
        Assert.Equal(2, store.Evidence.Count);
    }

    [Fact]
    public async Task Ingest_does_not_duplicate_evidence_from_the_same_message()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        var message = new Message(conversation.Id, MessageRole.User, "I like espresso.", 1);
        store.AddConversation(conversation);
        store.AddMessage(message);

        var extractor = Preference("User likes espresso.", 0.5m, 0.6m);
        var options = new MemoryAiOptions
        {
            RecentMessageWindow = 10,
            MinPersistConfidence = 0.5m,
            MinPersistImportance = 0.3m,
            AutoPromoteConfidence = 0.95m,
            AutoPromoteImportance = 0.95m,
            AutoPromoteEvidenceCount = 3
        };
        var embeddings = new FakeEmbeddingProvider { IsAvailable = false };
        var service = CreateService(store, extractor, embeddings, options);

        await service.IngestFromMessageAsync(conversation.Id, message.Id);
        await service.IngestFromMessageAsync(conversation.Id, message.Id);

        var pending = Assert.Single(store.Candidates);
        Assert.Equal(1, pending.EvidenceCount);
        Assert.Single(store.Evidence);
        Assert.Empty(store.Memories);
    }

    [Fact]
    public async Task Ingest_does_not_mutate_pending_candidate_when_embedding_fails()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        var firstMessage = new Message(conversation.Id, MessageRole.User, "I like espresso.", 1);
        var secondMessage = new Message(conversation.Id, MessageRole.User, "Espresso is my drink.", 2);
        store.AddConversation(conversation);
        store.AddMessage(firstMessage);
        store.AddMessage(secondMessage);

        var extractor = Preference("User likes espresso.", 0.5m, 0.6m);
        var options = new MemoryAiOptions
        {
            RecentMessageWindow = 10,
            MinPersistConfidence = 0.5m,
            MinPersistImportance = 0.3m,
            AutoPromoteConfidence = 0.95m,
            AutoPromoteImportance = 0.95m,
            AutoPromoteEvidenceCount = 2
        };
        var service = CreateService(store, extractor, new ThrowingEmbeddingProvider(), options);

        await service.IngestFromMessageAsync(conversation.Id, firstMessage.Id);

        var pending = Assert.Single(store.Candidates);
        Assert.Equal(1, pending.EvidenceCount);
        Assert.Single(store.Evidence);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.IngestFromMessageAsync(conversation.Id, secondMessage.Id));

        Assert.Empty(store.Memories);
        Assert.Equal(MemoryCandidateStatus.Pending, pending.Status);
        Assert.Equal(1, pending.EvidenceCount);
        Assert.Single(store.Evidence);
    }

    [Fact]
    public async Task Ingest_drops_owner_id_metadata_candidates()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        var message = new Message(conversation.Id, MessageRole.User, "Hello.", 1);
        store.AddConversation(conversation);
        store.AddMessage(message);

        var extractor = new FakeMemoryExtractor
        {
            IsAvailable = true,
            Candidates =
            [
                new ExtractedMemoryCandidate(
                    "Owner id is user-1.",
                    MemoryType.Fact,
                    MemoryScope.User,
                    0.9m,
                    0.95m,
                    null,
                    null,
                    null),
                new ExtractedMemoryCandidate(
                    conversation.OwnerId,
                    MemoryType.Fact,
                    MemoryScope.User,
                    0.9m,
                    0.95m,
                    null,
                    null,
                    null),
                new ExtractedMemoryCandidate(
                    "User's name is user-1.",
                    MemoryType.Fact,
                    MemoryScope.User,
                    0.9m,
                    0.95m,
                    null,
                    null,
                    "Owner id: user-1"),
                new ExtractedMemoryCandidate(
                    "User likes tea.",
                    MemoryType.Preference,
                    MemoryScope.User,
                    0.8m,
                    0.9m,
                    null,
                    null,
                    null)
            ]
        };

        var service = CreateService(
            store,
            extractor,
            new FakeEmbeddingProvider { IsAvailable = false });

        await service.IngestFromMessageAsync(conversation.Id, message.Id);

        var memory = Assert.Single(store.Memories);
        Assert.Equal("User likes tea.", memory.Content);
        Assert.DoesNotContain(store.Candidates, candidate => candidate.Content.Contains("owner id", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(store.Candidates, candidate => candidate.Content == conversation.OwnerId);
    }

    [Fact]
    public async Task Ingest_skips_low_confidence_candidates()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("user-1", "chat-1");
        var message = new Message(conversation.Id, MessageRole.User, "ok", 1);
        store.AddConversation(conversation);
        store.AddMessage(message);

        var service = CreateService(
            store,
            Preference("User might like jazz maybe.", 0.2m, 0.2m),
            new FakeEmbeddingProvider { IsAvailable = false },
            new MemoryAiOptions
            {
                MinPersistConfidence = 0.55m,
                MinPersistImportance = 0.35m
            });

        await service.IngestFromMessageAsync(conversation.Id, message.Id);

        Assert.Empty(store.Memories);
        Assert.Empty(store.Candidates);
    }

    private static MemoryIngestionService CreateService(
        FakeMemoryStore store,
        IMemoryExtractor extractor,
        IEmbeddingProvider embeddings,
        MemoryAiOptions? options = null,
        IContradictionDetector? detector = null)
    {
        return new MemoryIngestionService(
            store,
            extractor,
            embeddings,
            detector ?? new FakeContradictionDetector(),
            Options.Create(options ?? new MemoryAiOptions()),
            NullLogger<MemoryIngestionService>.Instance);
    }

    private static FakeMemoryExtractor StrongPreference(string content, string? sourceSummary = null)
    {
        return Preference(content, 0.8m, 0.9m, sourceSummary);
    }

    private static FakeMemoryExtractor Preference(
        string content,
        decimal importance,
        decimal confidence,
        string? sourceSummary = null)
    {
        return new FakeMemoryExtractor
        {
            IsAvailable = true,
            Candidates =
            [
                new ExtractedMemoryCandidate(
                    content,
                    MemoryType.Preference,
                    MemoryScope.User,
                    importance,
                    confidence,
                    null,
                    null,
                    sourceSummary)
            ]
        };
    }

    private static Task<IReadOnlyList<float>> Embed(string input)
    {
        return new DeterministicEmbeddingProvider().EmbedAsync(input);
    }

    private sealed class DeterministicEmbeddingProvider : IEmbeddingProvider
    {
        public bool IsAvailable => true;

        public Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<float>>([1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f]);
        }
    }

    private sealed class ThrowingEmbeddingProvider : IEmbeddingProvider
    {
        public bool IsAvailable => true;

        public Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Embedding failed.");
        }
    }
}
