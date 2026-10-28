using KukaFm.Middleware.Api.Missions;
using Microsoft.EntityFrameworkCore;

namespace KukaFm.Middleware.Api.Data;

public interface IMissionRepository
{
    /// <summary>Missions with their robot, optionally filtered, sorted created_at desc, id desc.</summary>
    Task<List<Mission>> ListAsync(MissionState? state, CancellationToken ct);
    /// <summary>Newest <paramref name="perMission"/> previous states of each given mission (with robot), newest first.</summary>
    Task<List<MissionStatusHistory>> ListHistoryAsync(IReadOnlyCollection<long> missionIds, int perMission, CancellationToken ct);
    /// <summary>One page of previous states of a mission (with robot), newest first, with the total row count.</summary>
    Task<(List<MissionStatusHistory> Items, int TotalCount)> ListHistoryPageAsync(long missionId, int page, int pageSize, CancellationToken ct);
    /// <summary>Tracked mission with its robot, or null.</summary>
    Task<Mission?> GetByCodeAsync(string missionCode, CancellationToken ct);
    /// <summary>Tracked missions with an active status.</summary>
    Task<List<Mission>> ListActiveAsync(CancellationToken ct);
    void Add(Mission mission);
    void AddHistory(MissionStatusHistory history);
    Task SaveChangesAsync(CancellationToken ct);
}

public class MissionRepository(AppDbContext db) : IMissionRepository
{
    public Task<List<Mission>> ListAsync(MissionState? state, CancellationToken ct)
    {
        var query = db.Missions.AsNoTracking().Include(m => m.Robot).AsQueryable();
        query = state switch
        {
            MissionState.Active => query.Where(m => MissionStatus.Active.Contains(m.Status)),
            MissionState.Finished => query.Where(m => !MissionStatus.Active.Contains(m.Status)),
            _ => query,
        };
        return query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).ToListAsync(ct);
    }

    public Task<List<MissionStatusHistory>> ListHistoryAsync(IReadOnlyCollection<long> missionIds, int perMission, CancellationToken ct) =>
        db.Missions.AsNoTracking()
            .Where(m => missionIds.Contains(m.Id))
            .SelectMany(m => db.MissionStatusHistory
                .Where(h => h.MissionId == m.Id)
                .OrderByDescending(h => h.ValidTo).ThenByDescending(h => h.Id)
                .Take(perMission))
            .Include(h => h.Robot)
            .ToListAsync(ct);

    public async Task<(List<MissionStatusHistory> Items, int TotalCount)> ListHistoryPageAsync(
        long missionId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.MissionStatusHistory.AsNoTracking().Where(h => h.MissionId == missionId);
        var total = await query.CountAsync(ct);
        var skip = Paging.Skip(page, pageSize);
        if (skip >= total)
            return ([], total);

        var items = await query
            .Include(h => h.Robot)
            .OrderByDescending(h => h.ValidTo).ThenByDescending(h => h.Id)
            .Skip((int)skip).Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<Mission?> GetByCodeAsync(string missionCode, CancellationToken ct) =>
        db.Missions.Include(m => m.Robot).SingleOrDefaultAsync(m => m.MissionCode == missionCode, ct);

    public Task<List<Mission>> ListActiveAsync(CancellationToken ct) =>
        db.Missions.Where(m => MissionStatus.Active.Contains(m.Status)).ToListAsync(ct);

    public void Add(Mission mission) => db.Missions.Add(mission);

    public void AddHistory(MissionStatusHistory history) => db.MissionStatusHistory.Add(history);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
