using System.Text.Json.Serialization;
using KukaFm.Middleware.Api.Data;
using KukaFm.Middleware.Api.Kuka;
using KukaFm.Middleware.Api.Sync;

namespace KukaFm.Middleware.Api.Missions;

public record DispatchMissionRequest(long? RobotId, string? StartNode, string? TargetNode);

public record MissionDto(
    long Id, string MissionCode, string MissionType, string StartNode, string TargetNode, long RobotId, string RobotCode,
    short Status, string? StatusName, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? CompletedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<MissionHistoryDto>? History = null);

public record MissionHistoryDto(
    string MissionCode, string MissionType, string StartNode, string TargetNode, long RobotId, string RobotCode,
    short Status, string? StatusName, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt,
    DateTimeOffset ValidFrom, DateTimeOffset ValidTo);

public enum MissionOutcome
{
    Success,
    Invalid,
    NotFound,
    NotCancelable,
    NotReady,
    KukaError,
}

/// <summary>Result of dispatch / cancel; endpoints map <see cref="Outcome"/> to the HTTP status.</summary>
public record MissionResult(
    MissionOutcome Outcome, MissionDto? Mission = null, Dictionary<string, string[]>? Errors = null, string? Detail = null);

public class MissionService(
    IMissionRepository missions, IRobotRepository robots, IKukaClient kuka, SyncReadiness readiness,
    TimeProvider time, ILogger<MissionService> logger)
{
    public const string MissionType = "RACK_MOVE";
    public const int MaxNodeLength = 64;
    public const string NotReadyDetail = "Synchronization not ready yet";

    /// <summary>Parses the <c>state</c> query value; null or empty means all missions.</summary>
    public static bool TryParseState(string? value, out MissionState? state)
    {
        state = null;
        switch (value)
        {
            case null or "":
                return true;
            case "active":
                state = MissionState.Active;
                return true;
            case "finished":
                state = MissionState.Finished;
                return true;
            default:
                return false;
        }
    }

    public async Task<List<MissionDto>> ListAsync(MissionState? state, bool includeHistory, CancellationToken ct)
    {
        var list = await missions.ListAsync(state, ct);
        var history = includeHistory && list.Count > 0
            ? (await missions.ListHistoryAsync(list.Select(m => m.Id).ToList(), Paging.IncludedHistoryRows, ct)).ToLookup(h => h.MissionId)
            : null;

        return list
            .Select(m => ToDto(m, m.Robot.RobotCode) with
            {
                History = history?[m.Id].OrderByDescending(h => h.ValidTo).ThenByDescending(h => h.Id).Select(ToDto).ToList(),
            })
            .ToList();
    }

    /// <summary>Returns null when the mission does not exist.</summary>
    public async Task<PagedResult<MissionHistoryDto>?> ListHistoryAsync(string missionCode, int page, int pageSize, CancellationToken ct)
    {
        if (await missions.GetByCodeAsync(missionCode, ct) is not { } mission)
            return null;

        var (items, total) = await missions.ListHistoryPageAsync(mission.Id, page, pageSize, ct);
        return new PagedResult<MissionHistoryDto>(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<MissionResult> DispatchAsync(DispatchMissionRequest request, CancellationToken ct)
    {
        if (!readiness.IsReady)
        {
            logger.LogWarning("Dispatch to robot {RobotId} rejected: {Detail}", request.RobotId, NotReadyDetail);
            return new(MissionOutcome.NotReady, Detail: NotReadyDetail);
        }

        var startNode = request.StartNode?.Trim() ?? "";
        var targetNode = request.TargetNode?.Trim() ?? "";
        var errors = new Dictionary<string, string[]>();
        ValidateNode(errors, "startNode", startNode);
        ValidateNode(errors, "targetNode", targetNode);

        Robot? robot = null;
        if (request.RobotId is not { } robotId)
            errors["robotId"] = ["robotId is required."];
        else if ((robot = await robots.GetAsync(robotId, ct)) is null)
            errors["robotId"] = [$"Robot with id {robotId} does not exist."];

        if (robot is null || errors.Count > 0)
        {
            logger.LogInformation("Dispatch to robot {RobotId} rejected: invalid input {@Errors}", request.RobotId, errors);
            return new(MissionOutcome.Invalid, Errors: errors);
        }

        var missionCode = NewCode();
        var requestId = NewCode();
        try
        {
            await kuka.SubmitMissionAsync(requestId, missionCode, robot.RobotCode, startNode, targetNode, ct);
        }
        catch (KukaException ex)
        {
            logger.LogError("Dispatch of mission {MissionCode} to robot {RobotCode} failed, nothing stored: KUKA error {Code}: {Message}",
                missionCode, robot.RobotCode, ex.Code, ex.Message);
            return new(MissionOutcome.KukaError, Detail: KukaErrorDetail(ex));
        }

        var now = time.GetUtcNow();
        var mission = new Mission
        {
            MissionCode = missionCode,
            RequestId = requestId,
            MissionType = MissionType,
            StartNode = startNode,
            TargetNode = targetNode,
            RobotId = robot.Id,
            Status = MissionStatus.Created,
            StatusName = MissionStatus.Name(MissionStatus.Created),
            CreatedAt = now,
            UpdatedAt = now,
        };
        missions.Add(mission);
        await missions.SaveChangesAsync(ct);

        logger.LogInformation("Mission {MissionCode} dispatched to robot {RobotCode} from {StartNode} to {TargetNode}",
            missionCode, robot.RobotCode, startNode, targetNode);
        return new(MissionOutcome.Success, ToDto(mission, robot.RobotCode));
    }

    public async Task<MissionResult> CancelAsync(string missionCode, CancellationToken ct)
    {
        if (!readiness.IsReady)
        {
            logger.LogWarning("Cancel of mission {MissionCode} rejected: {Detail}", missionCode, NotReadyDetail);
            return new(MissionOutcome.NotReady, Detail: NotReadyDetail);
        }

        if (await missions.GetByCodeAsync(missionCode, ct) is not { } mission)
        {
            logger.LogInformation("Cancel of mission {MissionCode} rejected: not found", missionCode);
            return new(MissionOutcome.NotFound, Detail: $"Mission {missionCode} does not exist.");
        }

        var robotCode = mission.Robot.RobotCode;
        if (!MissionStatus.IsCancelable(mission.Status))
        {
            logger.LogInformation("Cancel of mission {MissionCode} on robot {RobotCode} rejected: status {Status} {StatusName} is not cancelable",
                missionCode, robotCode, mission.Status, mission.StatusName);
            return new(MissionOutcome.NotCancelable,
                Detail: $"Mission {missionCode} has status {mission.Status} {mission.StatusName} and cannot be cancelled.");
        }

        try
        {
            await kuka.MissionCancelAsync(NewCode(), missionCode, ct);
        }
        catch (KukaException ex)
        {
            logger.LogError("Cancel of mission {MissionCode} on robot {RobotCode} failed: KUKA error {Code}: {Message}",
                missionCode, robotCode, ex.Code, ex.Message);
            return new(MissionOutcome.KukaError, Detail: KukaErrorDetail(ex));
        }

        logger.LogInformation("Mission {MissionCode} on robot {RobotCode} force-cancelled", missionCode, robotCode);
        return new(MissionOutcome.Success);
    }

    private static void ValidateNode(Dictionary<string, string[]> errors, string field, string value)
    {
        if (value.Length == 0)
            errors[field] = [$"{field} is required."];
        else if (value.Length > MaxNodeLength)
            errors[field] = [$"{field} must be at most {MaxNodeLength} characters."];
    }

    private static string KukaErrorDetail(KukaException ex) => $"KUKA FM error {ex.Code}: {ex.Message}";

    private static string NewCode() => Guid.NewGuid().ToString("N");

    private static MissionDto ToDto(Mission m, string robotCode) =>
        new(m.Id, m.MissionCode, m.MissionType, m.StartNode, m.TargetNode, m.RobotId, robotCode,
            m.Status, m.StatusName, MissionStatus.IsActive(m.Status), m.CreatedAt, m.UpdatedAt, m.CompletedAt);

    private static MissionHistoryDto ToDto(MissionStatusHistory h) =>
        new(h.MissionCode, h.MissionType, h.StartNode, h.TargetNode, h.RobotId, h.Robot.RobotCode,
            h.Status, h.StatusName, h.CreatedAt, h.CompletedAt, h.ValidFrom, h.ValidTo);
}
