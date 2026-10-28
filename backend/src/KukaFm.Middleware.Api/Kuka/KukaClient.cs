using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using KukaFm.Middleware.Api.Missions;
using Microsoft.Extensions.Options;

namespace KukaFm.Middleware.Api.Kuka;

public interface IKukaClient
{
    Task<IReadOnlyList<KukaRobot>> RobotQueryAsync(CancellationToken ct);

    /// <summary>Submits a RACK_MOVE mission from <paramref name="startNode"/> to <paramref name="targetNode"/>.</summary>
    Task SubmitMissionAsync(string requestId, string missionCode, string robotCode, string startNode, string targetNode, CancellationToken ct);

    /// <summary>Force-cancels a mission.</summary>
    Task MissionCancelAsync(string requestId, string missionCode, CancellationToken ct);

    /// <summary>Per-tick jobQuery with the configured limit and non-empty filters.</summary>
    Task<IReadOnlyList<KukaJob>> JobQueryAsync(CancellationToken ct);

    /// <summary>Fallback jobQuery for a single job code, without limit and filters.</summary>
    Task<IReadOnlyList<KukaJob>> JobQueryByCodeAsync(string jobCode, CancellationToken ct);
}

public class KukaClient : IKukaClient
{
    private readonly HttpClient _http;
    private readonly KukaFmOptions _options;
    private readonly ILogger<KukaClient> _logger;

    public KukaClient(HttpClient http, IOptions<KukaFmOptions> options, ILogger<KukaClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        _http.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
        // KUKA FM expects the raw token value without an auth scheme.
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", options.Value.AccessToken);
    }

    public async Task<IReadOnlyList<KukaRobot>> RobotQueryAsync(CancellationToken ct) =>
        await PostAsync<List<KukaRobot>>("robotQuery", new { }, ct) ?? [];

    public async Task SubmitMissionAsync(string requestId, string missionCode, string robotCode, string startNode, string targetNode, CancellationToken ct)
    {
        var o = _options.SubmitMission;
        await PostAsync<object>("submitMission", new
        {
            orgId = o.OrgId,
            requestId,
            missionCode,
            missionType = MissionService.MissionType,
            robotIds = new[] { robotCode },
            missionData = new[]
            {
                new { sequence = 1, position = startNode, type = o.NodeType, actionType = "UP_CONTAINER", passStrategy = o.PassStrategy, waitingMillis = o.WaitingMillis },
                new { sequence = 2, position = targetNode, type = o.NodeType, actionType = "DOWN_CONTAINER", passStrategy = o.PassStrategy, waitingMillis = o.WaitingMillis },
            },
        }, ct);
    }

    public async Task MissionCancelAsync(string requestId, string missionCode, CancellationToken ct) =>
        await PostAsync<object>("missionCancel", new
        {
            requestId,
            missionCode,
            containerCode = "",
            position = "",
            cancelMode = "FORCE",
            reason = "",
        }, ct);

    public async Task<IReadOnlyList<KukaJob>> JobQueryAsync(CancellationToken ct)
    {
        var q = _options.JobQuery;
        var body = new Dictionary<string, object> { ["limit"] = q.Limit };
        if (q.SourceValue is { } sourceValue)
            body["sourceValue"] = sourceValue;
        if (!string.IsNullOrWhiteSpace(q.CreateUsername))
            body["createUsername"] = q.CreateUsername;
        if (q.Maps.Length > 0)
            body["maps"] = q.Maps;
        return await PostAsync<List<KukaJob>>("jobQuery", body, ct) ?? [];
    }

    public async Task<IReadOnlyList<KukaJob>> JobQueryByCodeAsync(string jobCode, CancellationToken ct) =>
        await PostAsync<List<KukaJob>>("jobQuery", new { jobCode }, ct) ?? [];

    private async Task<T?> PostAsync<T>(string apiName, object request, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        KukaException error;
        try
        {
            using var response = await _http.PostAsJsonAsync($"interfaces/api/amr/{apiName}", request, ct);
            if (!response.IsSuccessStatusCode)
                throw new KukaException($"HTTP_{(int)response.StatusCode}", $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

            var envelope = await response.Content.ReadFromJsonAsync<KukaResponse<T>>(ct)
                ?? throw new KukaException("INVALID_RESPONSE", "Empty response body");
            if (!envelope.Success)
                throw new KukaException(envelope.Code ?? "", envelope.Message ?? "");

            _logger.LogInformation("KUKA {ApiName} succeeded in {DurationMs:F0} ms with code {Code}",
                apiName, Stopwatch.GetElapsedTime(started).TotalMilliseconds, envelope.Code);
            return envelope.Data;
        }
        catch (KukaException ex) { error = ex; }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested) { error = new("TIMEOUT", "Request timed out", ex); }
        catch (HttpRequestException ex) { error = new("HTTP_ERROR", ex.Message, ex); }
        catch (JsonException ex) { error = new("INVALID_RESPONSE", "Response is not a valid KUKA envelope", ex); }

        _logger.LogWarning("KUKA {ApiName} failed in {DurationMs:F0} ms with code {Code}: {Message}",
            apiName, Stopwatch.GetElapsedTime(started).TotalMilliseconds, error.Code, error.Message);
        throw error;
    }
}
