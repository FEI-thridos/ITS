using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace KukaFm.Middleware.Api.Kuka;

public class KukaFmOptions
{
    public const string Section = "KukaFm";

    [Required, Url]
    public string BaseUrl { get; set; } = "";

    public string AccessToken { get; set; } = "";

    [Range(1, int.MaxValue)]
    public int TimeoutSeconds { get; set; }

    [ValidateObjectMembers]
    public SubmitMissionOptions SubmitMission { get; set; } = new();

    [ValidateObjectMembers]
    public JobQueryOptions JobQuery { get; set; } = new();
}

/// <summary>Configured fields of the submitMission body (system-definition §4.1).</summary>
public class SubmitMissionOptions
{
    [Required]
    public string OrgId { get; set; } = "";

    [Required]
    public string NodeType { get; set; } = "";

    [Required]
    public string PassStrategy { get; set; } = "";

    [Range(0, int.MaxValue)]
    public int WaitingMillis { get; set; }
}

/// <summary>Limit and optional filters of the per-tick jobQuery (system-definition §4.3). Empty filters are not sent.</summary>
public class JobQueryOptions
{
    [Range(1, int.MaxValue)]
    public int Limit { get; set; }

    public int? SourceValue { get; set; }

    public string? CreateUsername { get; set; }

    public string[] Maps { get; set; } = [];
}
