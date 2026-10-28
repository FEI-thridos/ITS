using System.Text.Json.Serialization;
using KukaFm.Middleware.Api.Data;

namespace KukaFm.Middleware.Api.Robots;

public record RobotDto(
    long Id, string RobotCode, string? RobotType, short Status, string? StatusName, string State,
    decimal? BatteryLevel, string? MapCode, string? NodeCode, string? MissionCode, DateTimeOffset UpdatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<RobotHistoryDto>? History = null);

public record RobotHistoryDto(
    string RobotCode, string? RobotType, short Status, string? StatusName, string State,
    decimal? BatteryLevel, string? MapCode, string? NodeCode, string? MissionCode,
    DateTimeOffset ValidFrom, DateTimeOffset ValidTo);

public class RobotService(IRobotRepository robots)
{
    public async Task<List<RobotDto>> ListAsync(bool includeHistory, CancellationToken ct)
    {
        var list = await robots.ListAsync(ct);
        var history = includeHistory && list.Count > 0
            ? (await robots.ListHistoryAsync(list.Select(r => r.Id).ToList(), Paging.IncludedHistoryRows, ct)).ToLookup(h => h.RobotId)
            : null;

        return list
            .Select(r => new RobotDto(r.Id, r.RobotCode, r.RobotType, r.Status, r.StatusName, RobotStatus.State(r.Status),
                r.BatteryLevel, r.MapCode, r.NodeCode, r.MissionCode, r.UpdatedAt,
                history is null ? null : NewestFirst(history[r.Id])))
            .ToList();
    }

    /// <summary>Returns null when the robot does not exist.</summary>
    public async Task<PagedResult<RobotHistoryDto>?> ListHistoryAsync(long robotId, int page, int pageSize, CancellationToken ct)
    {
        if (await robots.GetAsync(robotId, ct) is null)
            return null;

        var (items, total) = await robots.ListHistoryPageAsync(robotId, page, pageSize, ct);
        return new PagedResult<RobotHistoryDto>(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    private static List<RobotHistoryDto> NewestFirst(IEnumerable<RobotStatusHistory> rows) =>
        rows.OrderByDescending(h => h.ValidTo).ThenByDescending(h => h.Id).Select(ToDto).ToList();

    private static RobotHistoryDto ToDto(RobotStatusHistory h) =>
        new(h.RobotCode, h.RobotType, h.Status, h.StatusName, RobotStatus.State(h.Status),
            h.BatteryLevel, h.MapCode, h.NodeCode, h.MissionCode, h.ValidFrom, h.ValidTo);
}
