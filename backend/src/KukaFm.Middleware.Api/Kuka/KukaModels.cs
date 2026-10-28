namespace KukaFm.Middleware.Api.Kuka;

public record KukaResponse<T>(T? Data, string? Code, string? Message, bool Success);

public record KukaRobot(
    string RobotId,
    string? RobotType,
    short Status,
    decimal? BatteryLevel,
    string? MapCode,
    string? NodeCode,
    string? MissionCode);

/// <summary>One jobQuery result row; only the fields the mission sync uses.</summary>
public record KukaJob(string JobCode, int Status, string? RobotId);
