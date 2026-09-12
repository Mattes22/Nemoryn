namespace Memory.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class MemorySchema
{
    public static async Task ApplyPendingMigrationsAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(MemorySchema));

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            if (!await dbContext.Database.CanConnectAsync(timeout.Token))
            {
                logger.LogWarning("Database is not reachable; skipped pending migrations.");
                return;
            }

            var pending = (await dbContext.Database.GetPendingMigrationsAsync(timeout.Token)).ToArray();
            if (pending.Length == 0)
            {
                return;
            }

            logger.LogInformation(
                "Applying {Count} pending migration(s): {Migrations}",
                pending.Length,
                string.Join(", ", pending));
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Database is not reachable; skipped pending migrations.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to apply database migrations.");
        }
    }
}
