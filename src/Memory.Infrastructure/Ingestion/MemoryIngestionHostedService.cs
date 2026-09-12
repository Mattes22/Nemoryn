namespace Memory.Infrastructure.Ingestion;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Memory.Application.Memories;
using Memory.Application.Runtime;

internal sealed class MemoryIngestionHostedService(
    IServiceScopeFactory scopeFactory,
    IRuntimeWorkerPulse workerPulse,
    ILogger<MemoryIngestionHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = false;
            string? error = null;
            workerPulse.HeartbeatIngestion();

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var ingestion = scope.ServiceProvider.GetRequiredService<IMemoryIngestionService>();
                processed = await ingestion.ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                logger.LogError(exception, "Memory ingestion worker failed.");
            }

            workerPulse.RecordIngestion(processed, error);

            var delay = processed ? TimeSpan.FromMilliseconds(250) : TimeSpan.FromSeconds(2);
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
