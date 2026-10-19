using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace KukaFm.Middleware.Api.Kuka;

public interface IKukaClient
{
    Task<IReadOnlyList<KukaRobot>> RobotQueryAsync(CancellationToken ct);
}

public class KukaClient : IKukaClient
{
    private readonly HttpClient _http;
    private readonly ILogger<KukaClient> _logger;

    public KukaClient(HttpClient http, IOptions<KukaFmOptions> options, ILogger<KukaClient> logger)
    {
        _http = http;
        _logger = logger;
        _http.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
        // KUKA FM expects the raw token value without an auth scheme.
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", options.Value.AccessToken);
    }

    public async Task<IReadOnlyList<KukaRobot>> RobotQueryAsync(CancellationToken ct) =>
        await PostAsync<List<KukaRobot>>("robotQuery", new { }, ct) ?? [];

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
