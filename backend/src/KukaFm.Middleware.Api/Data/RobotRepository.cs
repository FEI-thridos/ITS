using Microsoft.EntityFrameworkCore;

namespace KukaFm.Middleware.Api.Data;

public interface IRobotRepository
{
    /// <summary>All robots (tracked), ordered by robot_code (ordinal).</summary>
    Task<List<Robot>> ListAsync(CancellationToken ct);
    Task<Robot?> GetAsync(long id, CancellationToken ct);
    /// <summary>Newest <paramref name="perRobot"/> previous states of each given robot, newest first.</summary>
    Task<List<RobotStatusHistory>> ListHistoryAsync(IReadOnlyCollection<long> robotIds, int perRobot, CancellationToken ct);
    /// <summary>One page of previous states of a robot, newest first, with the total row count.</summary>
    Task<(List<RobotStatusHistory> Items, int TotalCount)> ListHistoryPageAsync(long robotId, int page, int pageSize, CancellationToken ct);
    void Add(Robot robot);
    void AddHistory(RobotStatusHistory history);
    Task SaveChangesAsync(CancellationToken ct);
}

public class RobotRepository(AppDbContext db) : IRobotRepository
{
    public Task<List<Robot>> ListAsync(CancellationToken ct) =>
        db.Robots.OrderBy(r => EF.Functions.Collate(r.RobotCode, "C")).ToListAsync(ct);

    public async Task<Robot?> GetAsync(long id, CancellationToken ct) =>
        await db.Robots.FindAsync([id], ct);

    public Task<List<RobotStatusHistory>> ListHistoryAsync(IReadOnlyCollection<long> robotIds, int perRobot, CancellationToken ct) =>
        db.Robots.AsNoTracking()
            .Where(r => robotIds.Contains(r.Id))
            .SelectMany(r => db.RobotStatusHistory
                .Where(h => h.RobotId == r.Id)
                .OrderByDescending(h => h.ValidTo).ThenByDescending(h => h.Id)
                .Take(perRobot))
            .ToListAsync(ct);

    public async Task<(List<RobotStatusHistory> Items, int TotalCount)> ListHistoryPageAsync(
        long robotId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.RobotStatusHistory.AsNoTracking().Where(h => h.RobotId == robotId);
        var total = await query.CountAsync(ct);
        var skip = Paging.Skip(page, pageSize);
        if (skip >= total)
            return ([], total);

        var items = await query
            .OrderByDescending(h => h.ValidTo).ThenByDescending(h => h.Id)
            .Skip((int)skip).Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public void Add(Robot robot) => db.Robots.Add(robot);

    public void AddHistory(RobotStatusHistory history) => db.RobotStatusHistory.Add(history);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
