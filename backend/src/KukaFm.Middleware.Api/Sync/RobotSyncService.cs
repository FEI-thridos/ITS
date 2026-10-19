using KukaFm.Middleware.Api.Data;
using KukaFm.Middleware.Api.Kuka;
using KukaFm.Middleware.Api.Robots;

namespace KukaFm.Middleware.Api.Sync;

/// <summary>One robot sync tick (UC8). Errors are logged and nothing is written; only cancellation propagates.</summary>
public class RobotSyncService(IKukaClient kuka, IRobotRepository robots, TimeProvider time, ILogger<RobotSyncService> logger)
{
    public async Task SyncAsync(CancellationToken ct)
    {
        try
        {
            var kukaRobots = await kuka.RobotQueryAsync(ct);
            var now = time.GetUtcNow();
            var existing = (await robots.ListAsync(ct)).ToDictionary(r => r.RobotCode);
            int inserted = 0, changed = 0;

            foreach (var k in kukaRobots)
            {
                if (!existing.TryGetValue(k.RobotId, out var robot))
                {
                    robot = new Robot { RobotCode = k.RobotId };
                    Apply(robot, k, now);
                    robots.Add(robot);
                    inserted++;
                }
                else if (HasChanged(robot, k))
                {
                    robots.AddHistory(new RobotStatusHistory
                    {
                        RobotId = robot.Id,
                        RobotCode = robot.RobotCode,
                        RobotType = robot.RobotType,
                        Status = robot.Status,
                        StatusName = robot.StatusName,
                        BatteryLevel = robot.BatteryLevel,
                        MapCode = robot.MapCode,
                        NodeCode = robot.NodeCode,
                        MissionCode = robot.MissionCode,
                        ValidFrom = robot.UpdatedAt,
                        ValidTo = now,
                    });
                    Apply(robot, k, now);
                    changed++;
                }
            }

            await robots.SaveChangesAsync(ct);
            logger.LogInformation("Robot sync: {Received} received, {Inserted} inserted, {Changed} changed",
                kukaRobots.Count, inserted, changed);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (KukaException ex)
        {
            logger.LogError("Robot sync failed, nothing written: KUKA error {Code}: {Message}", ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Robot sync failed, nothing written");
        }
    }

    private static bool HasChanged(Robot robot, KukaRobot k) =>
        robot.RobotType != k.RobotType
        || robot.Status != k.Status
        || robot.StatusName != RobotStatus.Name(k.Status)
        || robot.BatteryLevel != k.BatteryLevel
        || robot.MapCode != k.MapCode
        || robot.NodeCode != k.NodeCode
        || robot.MissionCode != k.MissionCode;

    private static void Apply(Robot robot, KukaRobot k, DateTimeOffset now)
    {
        robot.RobotType = k.RobotType;
        robot.Status = k.Status;
        robot.StatusName = RobotStatus.Name(k.Status);
        robot.BatteryLevel = k.BatteryLevel;
        robot.MapCode = k.MapCode;
        robot.NodeCode = k.NodeCode;
        robot.MissionCode = k.MissionCode;
        robot.UpdatedAt = now;
    }
}
