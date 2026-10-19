using Microsoft.Extensions.Options;

namespace KukaFm.Middleware.Api.Sync;

/// <summary>Runs the robot sync once at startup, then on the configured interval.</summary>
public class RobotSyncWorker(IServiceScopeFactory scopes, IOptions<SyncOptions> options, TimeProvider time) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.RobotIntervalSeconds), time);
        do
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<RobotSyncService>().SyncAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
