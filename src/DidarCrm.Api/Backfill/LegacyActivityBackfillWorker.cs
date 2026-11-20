using DidarCrm.Application.Backfill.ReleaseLegacyActivities;
using DidarCrm.Infrastructure.Backfill;

namespace DidarCrm.Api.Backfill;

internal sealed class LegacyActivityBackfillWorker(
    IServiceScopeFactory scopeFactory,
    LegacyActivityBackfillOptions options,
    ILogger<LegacyActivityBackfillWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider
                    .GetRequiredService<ReleaseLegacyActivitiesHandler>();
                var processedCount = await handler.HandleAsync(
                    options.BatchSize,
                    stoppingToken);

                if (processedCount > 0)
                {
                    logger.LogInformation(
                        "Released {ProcessedCount} legacy activities for asynchronous processing.",
                        processedCount);
                }

                var delay = processedCount == options.BatchSize
                    ? options.BatchDelay
                    : options.PollingInterval;
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Legacy activity backfill batch failed and will be retried.");
                await Task.Delay(options.PollingInterval, stoppingToken);
            }
        }
    }
}
