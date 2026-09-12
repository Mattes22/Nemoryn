namespace Memory.Application.Tests.Runtime;

using Memory.Application.Configuration;
using Memory.Application.Runtime;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Conversations;
using Memory.Domain.Ingestion;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

public sealed class RuntimeStatusServiceTests
{
    [Fact]
    public async Task Get_reports_live_memory_model_and_workers()
    {
        var store = SeedQueues();
        var pulse = new RuntimeWorkerPulse();
        pulse.RecordIngestion(true);
        pulse.RecordRetention(false);

        var status = await CreateService(store, pulse).GetAsync();

        Assert.Equal(RuntimeStatusService.Healthy, status.Overall);
        Assert.Equal(RuntimeStatusService.Live, status.Memory.Status);
        Assert.Equal("20260910152655_AddMemoryAuditLog", status.Memory.LatestMigration);
        Assert.Equal(1, status.Memory.PendingCandidates);
        Assert.Equal(1, status.Memory.PendingConflicts);
        Assert.Equal(RuntimeStatusService.Live, status.Model.Status);
        Assert.Equal("Ollama", status.Model.Provider);
        Assert.Equal("llama3.1", status.Model.ChatModel);
        Assert.Equal("nomic-embed-text", status.Model.EmbeddingModel);
        Assert.True(status.Model.EmbeddingAvailable);
        Assert.True(status.Model.ProviderReachable);
        Assert.Equal(RuntimeStatusService.Live, status.Workers.Status);
        Assert.Equal(1, status.Workers.PendingIngestionJobs);
        Assert.Equal(1, status.Workers.ProcessingIngestionJobs);
    }

    [Fact]
    public async Task Get_marks_memory_down_when_database_is_unreachable()
    {
        var store = new FakeMemoryStore { DatabaseCanConnect = false };
        var pulse = new RuntimeWorkerPulse();
        pulse.RecordIngestion(true);
        pulse.RecordRetention(true);

        var status = await CreateService(store, pulse).GetAsync();

        Assert.Equal(RuntimeStatusService.Unhealthy, status.Overall);
        Assert.Equal(RuntimeStatusService.Down, status.Memory.Status);
        Assert.False(status.Memory.DatabaseReachable);
        Assert.Equal(0, status.Memory.PendingCandidates);
    }

    [Fact]
    public async Task Get_marks_model_down_when_provider_probe_fails()
    {
        var store = new FakeMemoryStore { LatestMigration = "test" };
        var pulse = new RuntimeWorkerPulse();
        pulse.RecordIngestion(true);
        pulse.RecordRetention(true);
        var probe = new FakeAiRuntimeProbe
        {
            Result = new(false, false, 2000, null, "Connection refused")
        };

        var status = await CreateService(store, pulse, probe: probe).GetAsync();

        Assert.Equal(RuntimeStatusService.Degraded, status.Overall);
        Assert.Equal(RuntimeStatusService.Live, status.Memory.Status);
        Assert.Equal(RuntimeStatusService.Down, status.Model.Status);
        Assert.False(status.Model.ProviderReachable);
        Assert.Equal("Connection refused", status.Model.ProviderError);
    }

    [Fact]
    public async Task Get_marks_model_unconfigured_when_providers_are_off()
    {
        var store = new FakeMemoryStore();
        var pulse = new RuntimeWorkerPulse();
        pulse.RecordIngestion(true);
        pulse.RecordRetention(true);

        var status = await CreateService(
            store,
            pulse,
            chatAvailable: false,
            embeddingAvailable: false,
            extractorAvailable: false,
            probe: new FakeAiRuntimeProbe
            {
                Result = new(false, false, null, null, "AI provider is not configured.")
            }).GetAsync();

        Assert.Equal(RuntimeStatusService.Degraded, status.Overall);
        Assert.Equal(RuntimeStatusService.Unconfigured, status.Model.Status);
        Assert.False(status.Model.EmbeddingAvailable);
    }

    [Fact]
    public async Task Get_marks_workers_stalled_when_pulse_is_old()
    {
        var store = new FakeMemoryStore();
        var pulse = new RuntimeWorkerPulse();
        var stale = DateTimeOffset.UtcNow.AddHours(-1);
        pulse.RecordIngestion(true, at: stale);
        pulse.RecordRetention(true, at: stale);

        var status = await CreateService(store, pulse).GetAsync();

        Assert.Equal(RuntimeStatusService.Stalled, status.Workers.Ingestion.Status);
        Assert.Equal(RuntimeStatusService.Stalled, status.Workers.Retention.Status);
        Assert.Equal(RuntimeStatusService.Stalled, status.Workers.Status);
        Assert.Equal(RuntimeStatusService.Degraded, status.Overall);
    }

    [Fact]
    public async Task Get_marks_workers_starting_before_first_pulse()
    {
        var status = await CreateService(new FakeMemoryStore(), new RuntimeWorkerPulse()).GetAsync();

        Assert.Equal(RuntimeStatusService.Degraded, status.Overall);
        Assert.Equal(RuntimeStatusService.Starting, status.Workers.Status);
        Assert.Equal(RuntimeStatusService.Starting, status.Workers.Ingestion.Status);
    }

    private static FakeMemoryStore SeedQueues()
    {
        var store = new FakeMemoryStore { LatestMigration = "20260910152655_AddMemoryAuditLog" };
        var conversation = new Conversation("matej", "chat-1");
        var message = new Message(conversation.Id, MessageRole.User, "Ahoj", 1);
        var pendingJob = new MemoryIngestionJob(conversation.Id, message.Id);
        var processingJob = new MemoryIngestionJob(conversation.Id, message.Id);
        processingJob.MarkProcessing();
        var candidate = new MemoryCandidate(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User maybe likes jazz.",
            MemoryType.Preference,
            0.4m,
            0.5m);
        var memory = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User lives in Brno.",
            MemoryType.Fact,
            0.9m,
            0.9m);
        var conflict = new MemoryConflict(
            conversation.OwnerId,
            conversation.Id,
            candidate.Id,
            memory.Id,
            0.8m,
            "review");
        store.AddConversation(conversation);
        store.AddMessage(message);
        store.AddIngestionJob(pendingJob);
        store.AddIngestionJob(processingJob);
        store.AddMemory(memory);
        store.AddMemoryCandidate(candidate);
        store.AddMemoryConflict(conflict);
        return store;
    }

    private static RuntimeStatusService CreateService(
        FakeMemoryStore store,
        RuntimeWorkerPulse pulse,
        bool chatAvailable = true,
        bool embeddingAvailable = true,
        bool extractorAvailable = true,
        FakeAiRuntimeProbe? probe = null)
    {
        return new RuntimeStatusService(
            store,
            new FakeEmbeddingProvider { IsAvailable = embeddingAvailable },
            new FakeChatCompletionProvider { IsAvailable = chatAvailable },
            new FakeMemoryExtractor { IsAvailable = extractorAvailable },
            probe ?? new FakeAiRuntimeProbe(),
            pulse,
            new FakeOptionsMonitor<MemoryAiOptions>(new MemoryAiOptions
            {
                Provider = "Ollama",
                BaseUrl = "http://localhost:11434",
                ChatModel = "llama3.1",
                EmbeddingModel = "nomic-embed-text",
                RetentionPollSeconds = 900
            }));
    }
}
