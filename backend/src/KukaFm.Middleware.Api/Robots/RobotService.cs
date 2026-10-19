using KukaFm.Middleware.Api.Data;

namespace KukaFm.Middleware.Api.Robots;

public record RobotDto(
    long Id, string RobotCode, string? RobotType, short Status, string? StatusName, string State,
    decimal? BatteryLevel, string? MapCode, string? NodeCode, string? MissionCode, DateTimeOffset UpdatedAt);

public record RobotHistoryDto(
    string RobotCode, string? RobotType, short Status, string? StatusName, string State,
    decimal? BatteryLevel, string? MapCode, string? NodeCode, string? MissionCode,
    DateTimeOffset ValidFrom, DateTimeOffset ValidTo);

public class RobotService(IRobotRepository robots)
{
    public const int MaxHistoryRows = 50;

    public async Task<List<RobotDto>> ListAsync(CancellationToken ct) =>
        (await robots.ListAsync(ct))
            .Select(r => new RobotDto(r.Id, r.RobotCode, r.RobotType, r.Status, r.StatusName, RobotStatus.State(r.Status),
                r.BatteryLevel, r.MapCode, r.NodeCode, r.MissionCode, r.UpdatedAt))
            .ToList();

    /// <summary>Returns null when the robot does not exist.</summary>
    public async Task<List<RobotHistoryDto>?> ListHistoryAsync(long robotId, CancellationToken ct)
    {
        if (!await robots.ExistsAsync(robotId, ct))
            return null;

        return (await robots.ListHistoryAsync(robotId, MaxHistoryRows, ct))
            .Select(h => new RobotHistoryDto(h.RobotCode, h.RobotType, h.Status, h.StatusName, RobotStatus.State(h.Status),
                h.BatteryLevel, h.MapCode, h.NodeCode, h.MissionCode, h.ValidFrom, h.ValidTo))
            .ToList();
    }
}
