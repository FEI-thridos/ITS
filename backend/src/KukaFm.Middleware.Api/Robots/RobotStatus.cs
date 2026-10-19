namespace KukaFm.Middleware.Api.Robots;

/// <summary>KUKA robot status codes and mapped state (system-definition §4.4). KUKA FM only sends codes 1–7.</summary>
public static class RobotStatus
{
    public const string Available = "Available";
    public const string Busy = "Busy";
    public const string Offline = "Offline";

    public static string Name(short status) => status switch
    {
        1 => "Departure",
        2 => "Offline",
        3 => "Idle",
        4 => "Executing",
        5 => "Charging",
        6 => "Updating",
        7 => "Abnormal",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static string State(short status) => status switch
    {
        3 => Available,
        1 or 4 or 5 or 6 => Busy,
        2 or 7 => Offline,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
