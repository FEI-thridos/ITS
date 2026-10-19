using System.ComponentModel.DataAnnotations;

namespace KukaFm.Middleware.Api.Sync;

public class SyncOptions
{
    public const string Section = "Sync";

    [Range(1, int.MaxValue)]
    public int RobotIntervalSeconds { get; set; }
}
