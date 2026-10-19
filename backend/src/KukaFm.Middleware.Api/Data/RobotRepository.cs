using Microsoft.EntityFrameworkCore;

namespace KukaFm.Middleware.Api.Data;

public interface IRobotRepository
{
    /// <summary>All robots (tracked), ordered by robot_code (ordinal).</summary>
    Task<List<Robot>> ListAsync(CancellationToken ct);
    Task<bool> ExistsAsync(long id, CancellationToken ct);
    /// <summary>Previous states of a robot, newest first.</summary>
    Task<List<RobotStatusHistory>> ListHistoryAsync(long robotId, int maxRows, CancellationToken ct);
    void Add(Robot robot);
    void AddHistory(RobotStatusHistory history);
    Task SaveChangesAsync(CancellationToken ct);
}

public class RobotRepository(AppDbContext db) : IRobotRepository
{
    public Task<List<Robot>> ListAsync(CancellationToken ct) =>
        db.Robots.OrderBy(r => EF.Functions.Collate(r.RobotCode, "C")).ToListAsync(ct);

    public Task<bool> ExistsAsync(long id, CancellationToken ct) =>
        db.Robots.AnyAsync(r => r.Id == id, ct);

    public Task<List<RobotStatusHistory>> ListHistoryAsync(long robotId, int maxRows, CancellationToken ct) =>
        db.RobotStatusHistory.AsNoTracking()
            .Where(h => h.RobotId == robotId)
            .OrderByDescending(h => h.ValidTo).ThenByDescending(h => h.Id)
            .Take(maxRows)
            .ToListAsync(ct);

    public void Add(Robot robot) => db.Robots.Add(robot);

    public void AddHistory(RobotStatusHistory history) => db.RobotStatusHistory.Add(history);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
