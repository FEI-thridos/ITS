namespace KukaFm.Middleware.Api.Missions;

/// <summary>KUKA job status codes (system-definition §4.3). Any other code is invalid data.</summary>
public static class MissionStatus
{
    public const short Created = 10;

    /// <summary>Statuses still polled by the mission sync.</summary>
    public static readonly short[] Active = [10, 20, 25, 28, 50];

    /// <summary>Statuses a mission can be force-cancelled from (28 Cancelling is already being cancelled).</summary>
    public static readonly short[] Cancelable = [10, 20, 25, 50];

    public static readonly short[] Final = [30, 31, 35, 60];

    public static string Name(int status) => status switch
    {
        10 => "Created",
        20 => "Executing",
        25 => "Waiting",
        28 => "Cancelling",
        30 => "Complete",
        31 => "Cancelled",
        35 => "Manual complete",
        50 => "Warning",
        60 => "Startup error",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown KUKA job status"),
    };

    public static bool IsActive(short status) => Active.Contains(status);

    public static bool IsCancelable(short status) => Cancelable.Contains(status);

    public static bool IsFinal(short status) => Final.Contains(status);
}

/// <summary>Filter of <c>GET /api/missions?state=</c>: active = active statuses, finished = all others.</summary>
public enum MissionState
{
    Active,
    Finished,
}
