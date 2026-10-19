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
