namespace Memory.Application.Runtime;

using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.AI;
using Memory.Application.Abstractions.Persistence;
using Memory.Application.Configuration;

internal sealed class RuntimeStatusService(
    IMemoryStore memoryStore,
    IEmbeddingProvider embeddingProvider,
    IChatCompletionProvider chatCompletionProvider,
    IMemoryExtractor memoryExtractor,
    IAiRuntimeProbe aiRuntimeProbe,
    IRuntimeWorkerPulse workerPulse,
    IOptionsMonitor<MemoryAiOptions> memoryAiOptions) : IRuntimeStatusService
{
    public const string Live = "live";
    public const string Down = "down";
    public const string Unconfigured = "unconfigured";
    public const string Starting = "starting";
    public const string Stalled = "stalled";
    public const string Healthy = "Healthy";
    public const string Degraded = "Degraded";
    public const string Unhealthy = "Unhealthy";

    public static readonly TimeSpan IngestionLiveWindow = TimeSpan.FromSeconds(45);

    public async Task<RuntimeStatusResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var options = memoryAiOptions.CurrentValue;
        var databaseTask = memoryStore.GetDatabaseRuntimeAsync(cancellationToken);
        var probeTask = aiRuntimeProbe.ProbeAsync(cancellationToken);
        await Task.WhenAll(databaseTask, probeTask);

        var database = await databaseTask;
        var probe = await probeTask;
        var queues = database.CanConnect
            ? await memoryStore.GetRuntimeQueueCountsAsync(cancellationToken)
            : new RuntimeQueueCounts(0, 0, 0, 0);

        var memory = ToMemoryStatus(database, queues);
        var model = ToModelStatus(options, probe);
        var workers = ToWorkerStatus(options, queues, workerPulse.Ingestion, workerPulse.Retention);
        var overall = memory.Status != Live
            ? Unhealthy
            : model.Status == Live && workers.Status == Live
                ? Healthy
                : Degraded;

        return new RuntimeStatusResponse(DateTimeOffset.UtcNow, overall, memory, model, workers);
    }

    internal static TimeSpan RetentionLiveWindow(MemoryAiOptions options)
    {
        return TimeSpan.FromSeconds(Math.Max(45, options.RetentionPollSeconds + 15));
    }

    private static RuntimeMemoryStatus ToMemoryStatus(DatabaseRuntimeInfo database, RuntimeQueueCounts queues)
    {
        if (!database.CanConnect)
        {
            return new RuntimeMemoryStatus(
                Down,
                "Databáze neodpovídá.",
                false,
                null,
                0,
                0);
        }

        return new RuntimeMemoryStatus(
            Live,
            database.LatestMigration is null
                ? "Databáze žije, migrace se nepodařilo přečíst."
                : $"Databáze žije · {database.LatestMigration}",
            true,
            database.LatestMigration,
            queues.PendingCandidates,
            queues.PendingConflicts);
    }

    private RuntimeModelStatus ToModelStatus(MemoryAiOptions options, AiRuntimeProbeResult probe)
    {
        var chatAvailable = chatCompletionProvider.IsAvailable;
        var embeddingAvailable = embeddingProvider.IsAvailable;
        var extractorAvailable = memoryExtractor.IsAvailable;
        var configured = chatAvailable || embeddingAvailable || extractorAvailable;
        var status = !configured
            ? Unconfigured
            : probe.Healthy ? Live : Down;
        var summary = status switch
        {
            Unconfigured => "AI provider není nastavený.",
            Live => $"Model žije · {options.Provider} {options.ChatModel}",
            _ => probe.Error is null
                ? $"{options.Provider} neodpovídá."
                : $"{options.Provider}: {probe.Error}"
        };

        return new RuntimeModelStatus(
            status,
            summary,
            options.Provider,
            options.BaseUrl,
            options.ChatModel,
            options.EmbeddingModel,
            chatAvailable,
            embeddingAvailable,
            extractorAvailable,
            probe.Reachable,
            probe.LatencyMs,
            probe.Error);
    }

    private static RuntimeWorkerStatus ToWorkerStatus(
        MemoryAiOptions options,
        RuntimeQueueCounts queues,
        RuntimeWorkerPulseSnapshot ingestionPulse,
        RuntimeWorkerPulseSnapshot retentionPulse)
    {
        var ingestion = ToWorkerDetail("ingestion", ingestionPulse, IngestionLiveWindow);
        var retention = ToWorkerDetail("retention", retentionPulse, RetentionLiveWindow(options));
        var status = CombineWorkerStatus(ingestion.Status, retention.Status);
        var summary = status switch
        {
            Live => "Ingestion i retention běží.",
            Starting => "Workery startují.",
            Stalled => "Worker neodpovídá v očekávaném intervalu.",
            _ => "Workery neběží."
        };

        return new RuntimeWorkerStatus(
            status,
            summary,
            ingestion,
            retention,
            queues.PendingIngestionJobs,
            queues.ProcessingIngestionJobs);
    }

    private static RuntimeWorkerDetail ToWorkerDetail(
        string name,
        RuntimeWorkerPulseSnapshot pulse,
        TimeSpan liveWindow)
    {
        var status = pulse.LastAttemptAt is not DateTimeOffset attempted
            ? Starting
            : DateTimeOffset.UtcNow - attempted > liveWindow
                ? Stalled
                : Live;

        return new RuntimeWorkerDetail(
            name,
            status,
            pulse.LastAttemptAt,
            pulse.LastSuccessAt,
            pulse.LastSucceeded,
            pulse.LastError);
    }

    private static string CombineWorkerStatus(string ingestion, string retention)
    {
        if (ingestion == Stalled || retention == Stalled)
        {
            return Stalled;
        }

        if (ingestion == Live && retention == Live)
        {
            return Live;
        }

        if (ingestion == Starting && retention == Starting)
        {
            return Starting;
        }

        if (ingestion == Live || retention == Live)
        {
            return Live;
        }

        return Down;
    }
}
