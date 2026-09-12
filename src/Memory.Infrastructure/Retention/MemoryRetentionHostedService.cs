namespace Memory.Infrastructure.Retention;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Memory.Application.Configuration;
using Memory.Application.Memories;
using Memory.Application.Runtime;

internal sealed class MemoryRetentionHostedService(
    IServiceScopeFactory scopeFactory,
    IRuntimeWorkerPulse workerPulse,
    IOptionsMonitor<MemoryAiOptions> memoryAiOptions,
    ILogger<MemoryRetentionHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = false;
            string? error = null;
            workerPulse.HeartbeatRetention();

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var retention = scope.ServiceProvider.GetRequiredService<IMemoryRetentionService>();
                var result = await retention.ApplyAsync(stoppingToken);
                processed = result.ArchivedMemories > 0
                    || result.DiscardedCandidates > 0
                    || result.DeletedJobs > 0;

                if (processed)
                {
                    logger.LogInformation(
                        "Memory retention archived {ArchivedMemories} memories, discarded {DiscardedCandidates} candidates, and deleted {DeletedJobs} ingestion jobs.",
                        result.ArchivedMemories,
                        result.DiscardedCandidates,
                        result.DeletedJobs);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                logger.LogError(exception, "Memory retention worker failed.");
            }

            workerPulse.RecordRetention(processed, error);

            var delay = processed
                ? TimeSpan.FromMilliseconds(250)
                : TimeSpan.FromSeconds(Math.Max(30, memoryAiOptions.CurrentValue.RetentionPollSeconds));
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
