using Microsoft.Extensions.Options;

namespace KukaFm.Middleware.Api.Sync;

/// <summary>Waits for the first successful robot sync, then runs the mission sync immediately and on the configured interval.</summary>
public class MissionSyncWorker(
    IServiceScopeFactory scopes, SyncReadiness readiness, IOptions<SyncOptions> options, TimeProvider time, ILogger<MissionSyncWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Mission sync waiting for the first successful robot sync");
        await readiness.WaitForRobotsAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.MissionIntervalSeconds), time);
        do
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<MissionSyncService>().SyncAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
