using System.Net;
using System.Text;
using KukaFm.Middleware.Api.Kuka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace KukaFm.Middleware.Api.Tests.Kuka;

public class KukaClientTests
{
    private const string Token = "secret-token-123";
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly ILogger<KukaClient> _logger = Substitute.For<ILogger<KukaClient>>();

    private (KukaClient Client, StubHandler Handler) Create(Func<HttpResponseMessage> respond) =>
        Create(_ => Task.FromResult(respond()));

    private (KukaClient Client, StubHandler Handler) Create(Func<CancellationToken, Task<HttpResponseMessage>> respond)
    {
        var handler = new StubHandler(respond);
        var options = Options.Create(new KukaFmOptions { BaseUrl = "https://kukafm.test", AccessToken = Token, TimeoutSeconds = 10 });
        return (new KukaClient(new HttpClient(handler), options, _logger), handler);
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task RobotQuery_Success_MapsRobots()
    {
        var (client, _) = Create(() => Json("""
            {"data":[{"robotId":"R1","robotType":"LIFT","mapCode":"M1","status":3,"batteryLevel":85.5,"nodeCode":"N1","missionCode":null,"x":"1.0"}],
             "code":"0","message":null,"success":true}
            """));

        var robot = Assert.Single(await client.RobotQueryAsync(_ct));

        Assert.Equal(new KukaRobot("R1", "LIFT", 3, 85.5m, "M1", "N1", null), robot);
    }

    [Fact]
    public async Task RobotQuery_PostsEmptyBodyToApiPathWithRawAuthorizationHeader()
    {
        var (client, handler) = Create(() => Json("""{"data":[],"code":"0","message":null,"success":true}"""));

        await client.RobotQueryAsync(_ct);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://kukafm.test/interfaces/api/amr/robotQuery", handler.Request.RequestUri!.ToString());
        Assert.Equal(Token, Assert.Single(handler.Request.Headers.GetValues("Authorization")));
        Assert.Equal("application/json", handler.Request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("{}", handler.Body);
    }

    [Theory]
    [InlineData("""{"data":[],"code":"0","message":null,"success":true}""")]
    [InlineData("""{"data":null,"code":"0","message":null,"success":true}""")]
    [InlineData("""{"code":"0","success":true}""")]
    public async Task RobotQuery_EmptyOrNullData_ReturnsEmptyList(string json)
    {
        var (client, _) = Create(() => Json(json));

        Assert.Empty(await client.RobotQueryAsync(_ct));
    }

    [Fact]
    public async Task RobotQuery_SuccessFalse_ThrowsWithKukaCodeAndMessage()
    {
        var (client, _) = Create(() => Json("""{"data":null,"code":"100001","message":"unexpected error","success":false}"""));

        var ex = await Assert.ThrowsAsync<KukaException>(() => client.RobotQueryAsync(_ct));

        Assert.Equal("100001", ex.Code);
        Assert.Equal("unexpected error", ex.Message);
    }

    [Fact]
    public async Task RobotQuery_Http500_Throws()
    {
        var (client, _) = Create(() => Json("oops", HttpStatusCode.InternalServerError));

        var ex = await Assert.ThrowsAsync<KukaException>(() => client.RobotQueryAsync(_ct));

        Assert.Equal("HTTP_500", ex.Code);
    }

    [Fact]
    public async Task RobotQuery_Timeout_Throws()
    {
        var (client, _) = Create(_ => throw new TaskCanceledException("timeout", new TimeoutException()));

        var ex = await Assert.ThrowsAsync<KukaException>(() => client.RobotQueryAsync(_ct));

        Assert.Equal("TIMEOUT", ex.Code);
    }

    [Fact]
    public async Task RobotQuery_TransportError_Throws()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("connection refused"));

        var ex = await Assert.ThrowsAsync<KukaException>(() => client.RobotQueryAsync(_ct));

        Assert.Equal("HTTP_ERROR", ex.Code);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"data":"not-an-array","code":"0","success":true}""")]
    [InlineData("null")]
    public async Task RobotQuery_MalformedResponse_Throws(string body)
    {
        var (client, _) = Create(() => Json(body));

        var ex = await Assert.ThrowsAsync<KukaException>(() => client.RobotQueryAsync(_ct));

        Assert.Equal("INVALID_RESPONSE", ex.Code);
    }

    [Fact]
    public async Task RobotQuery_CallerCancelled_ThrowsCancellationNotKukaError()
    {
        using var cts = new CancellationTokenSource();
        var (client, _) = Create(async ct => { await cts.CancelAsync(); ct.ThrowIfCancellationRequested(); return Json("{}"); });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.RobotQueryAsync(cts.Token));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RobotQuery_NeverLogsToken(bool success)
    {
        var (client, _) = Create(() => Json(success
            ? """{"data":[],"code":"0","message":null,"success":true}"""
            : """{"data":null,"code":"100001","message":"bad","success":false}"""));

        try { await client.RobotQueryAsync(_ct); } catch (KukaException) { }

        var loggedValues = _logger.ReceivedCalls().SelectMany(c => c.GetArguments()).Select(a => a?.ToString() ?? "").ToList();
        Assert.NotEmpty(_logger.ReceivedCalls());
        Assert.DoesNotContain(loggedValues, v => v.Contains(Token));
    }

    private sealed class StubHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await respond(cancellationToken);
        }
    }
}
