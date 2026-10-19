using System.ComponentModel.DataAnnotations;

namespace KukaFm.Middleware.Api.Kuka;

public class KukaFmOptions
{
    public const string Section = "KukaFm";

    [Required, Url]
    public string BaseUrl { get; set; } = "";

    public string AccessToken { get; set; } = "";

    [Range(1, int.MaxValue)]
    public int TimeoutSeconds { get; set; }
}
