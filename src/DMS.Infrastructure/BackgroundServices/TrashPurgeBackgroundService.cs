using DMS.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DMS.Infrastructure.BackgroundServices;

/// <summary>
/// Nightly background job that permanently deletes files and folders whose
/// DeletedAt timestamp is older than the configured retention period (default 30 days).
///
/// Why BackgroundService + PeriodicTimer instead of a hosted IHostedService with Task.Delay?
///   PeriodicTimer fires on a wall-clock interval and does not drift — each tick
///   is aligned to the interval from the previous fire time, not from when the
///   previous work completed. Task.Delay drifts forward by the duration of the work.
///   PeriodicTimer also supports CancellationToken directly on WaitForNextTickAsync,
///   which means the timer wakes up immediately on host shutdown rather than waiting
///   for the next interval.
///
/// Why IServiceScopeFactory instead of injecting ITrashPurgeService directly?
///   ITrashPurgeService depends on EF Core repositories (Scoped lifetime). A BackgroundService
///   is registered as a Singleton by the host. A Singleton cannot directly consume a Scoped
///   service — doing so would cause the Scoped service (and its DbContext) to live forever,
///   leaking connections and tracking stale data. Creating a new scope per invocation is the
///   correct pattern for Singleton → Scoped consumption.
/// </summary>
internal sealed class TrashPurgeBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<TrashPurgeBackgroundService> logger) : BackgroundService
{
    private const int RetentionDays  = 30;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var purgeService = scope.ServiceProvider.GetRequiredService<ITrashPurgeService>();
                await purgeService.PurgeExpiredItemsAsync(RetentionDays, stoppingToken);

                logger.LogInformation("Trash purge completed (retention = {Days} days)", RetentionDays);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Trash purge failed — will retry at next scheduled tick");
            }
        }
    }
}
