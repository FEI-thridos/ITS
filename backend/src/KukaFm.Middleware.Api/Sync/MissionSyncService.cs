using KukaFm.Middleware.Api.Data;
using KukaFm.Middleware.Api.Kuka;
using KukaFm.Middleware.Api.Missions;

namespace KukaFm.Middleware.Api.Sync;

/// <summary>
/// One mission sync tick (UC5). Errors (KUKA, DB, invalid data) are logged and nothing is written; only cancellation
/// propagates. The first successful tick marks the missions as ready for dispatch / cancel.
/// </summary>
public class MissionSyncService(
    IKukaClient kuka, IMissionRepository missions, IRobotRepository robots, SyncReadiness readiness,
    TimeProvider time, ILogger<MissionSyncService> logger)
{
    public async Task SyncAsync(CancellationToken ct)
    {
        try
        {
            var active = await missions.ListActiveAsync(ct);
            if (active.Count == 0)
            {
                logger.LogInformation("Mission sync: no active missions");
                MarkReady();
                return;
            }

            var jobs = new Dictionary<string, KukaJob>();
            foreach (var job in await kuka.JobQueryAsync(ct))
                jobs.TryAdd(job.JobCode, job);

            int fallbacks = 0, notFound = 0;
            foreach (var mission in active.Where(m => !jobs.ContainsKey(m.MissionCode)))
            {
                fallbacks++;
                var job = (await kuka.JobQueryByCodeAsync(mission.MissionCode, ct)).FirstOrDefault(j => j.JobCode == mission.MissionCode);
                if (job is null)
                {
                    notFound++;
                    logger.LogWarning("Mission sync: mission {MissionCode} not returned by KUKA FM, left unchanged", mission.MissionCode);
                    continue;
                }
                jobs[job.JobCode] = job;
            }

            var robotIds = (await robots.ListAsync(ct)).ToDictionary(r => r.RobotCode, r => r.Id);
            var now = time.GetUtcNow();
            int changed = 0, completed = 0;

            foreach (var mission in active)
            {
                if (!jobs.TryGetValue(mission.MissionCode, out var job))
                    continue;

                var statusName = MissionStatus.Name(job.Status);
                // No robot reported by KUKA keeps the current robot; a reported robot must exist in the DB.
                var robotId = mission.RobotId;
                if (!string.IsNullOrEmpty(job.RobotId) && !robotIds.TryGetValue(job.RobotId, out robotId))
                    throw new InvalidOperationException(
                        $"Mission {mission.MissionCode} reports robot '{job.RobotId}' that does not exist in the database");

                var status = (short)job.Status;
                if (status == mission.Status && robotId == mission.RobotId)
                    continue;

                missions.AddHistory(new MissionStatusHistory
                {
                    MissionId = mission.Id,
                    MissionCode = mission.MissionCode,
                    RequestId = mission.RequestId,
                    MissionType = mission.MissionType,
                    StartNode = mission.StartNode,
                    TargetNode = mission.TargetNode,
                    RobotId = mission.RobotId,
                    Status = mission.Status,
                    StatusName = mission.StatusName,
                    CreatedAt = mission.CreatedAt,
                    CompletedAt = mission.CompletedAt,
                    ValidFrom = mission.UpdatedAt,
                    ValidTo = now,
                });
                mission.Status = status;
                mission.StatusName = statusName;
                mission.RobotId = robotId;
                mission.UpdatedAt = now;
                changed++;
                if (MissionStatus.IsFinal(status))
                {
                    mission.CompletedAt = now;
                    completed++;
                }
            }

            await missions.SaveChangesAsync(ct);
            logger.LogInformation(
                "Mission sync: {Active} active, {Fallbacks} fallback queries, {NotFound} not found, {Changed} changed, {Completed} completed",
                active.Count, fallbacks, notFound, changed, completed);
            MarkReady();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (KukaException ex)
        {
            logger.LogError("Mission sync failed, nothing written: KUKA error {Code}: {Message}", ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Mission sync failed, nothing written");
        }
    }

    private void MarkReady()
    {
        if (readiness.MarkMissionsReady())
            logger.LogInformation("First mission sync succeeded, dispatch and cancel are enabled");
    }
}
