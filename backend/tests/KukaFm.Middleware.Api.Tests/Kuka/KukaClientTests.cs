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

    private (KukaClient Client, StubHandler Handler) Create(Func<CancellationToken, Task<HttpResponseMessage>> respond, JobQueryOptions? jobQuery = null)
    {
        var handler = new StubHandler(respond);
        var options = Options.Create(new KukaFmOptions
        {
            BaseUrl = "https://kukafm.test",
            AccessToken = Token,
            TimeoutSeconds = 10,
            SubmitMission = new SubmitMissionOptions { OrgId = "Labak", NodeType = "NODE_POINT", PassStrategy = "AUTO", WaitingMillis = 0 },
            JobQuery = jobQuery ?? new JobQueryOptions { Limit = 100 },
        });
        return (new KukaClient(new HttpClient(handler), options, _logger), handler);
    }

    private const string Ok = """{"data":null,"code":"0","message":null,"success":true}""";
    private const string Failed = """{"data":null,"code":"100409","message":"mission code duplicate","success":false}""";

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

    private object?[] SingleLog(LogLevel level) =>
        Assert.Single(_logger.ReceivedCalls(), c => c.GetArguments() is [LogLevel l, ..] && l == level).GetArguments();

    private static Dictionary<string, object?> Values(object?[] log) =>
        ((IReadOnlyList<KeyValuePair<string, object?>>)log[2]!).ToDictionary(kv => kv.Key, kv => kv.Value);

    [Fact]
    public async Task Failure_LogsWarningWithApiNameAndCodeButNotToken()
    {
        var (client, _) = Create(() => Json(Failed));

        await Assert.ThrowsAsync<KukaException>(() => client.SubmitMissionAsync("req1", "mc1", "R1", "A", "B", _ct));

        var warning = SingleLog(LogLevel.Warning);
        var values = Values(warning);
        Assert.Equal(("submitMission", "100409", "mission code duplicate"), (values["ApiName"], values["Code"], values["Message"]));
        Assert.DoesNotContain(values.Values, v => (v?.ToString() ?? "").Contains(Token));
        Assert.DoesNotContain(Token, warning[2]!.ToString());
    }

    [Fact]
    public async Task Success_LogsInformationWithApiNameAndDuration()
    {
        var (client, _) = Create(() => Json(Ok));

        await client.MissionCancelAsync("req2", "mc1", _ct);

        var values = Values(SingleLog(LogLevel.Information));
        Assert.Equal("missionCancel", values["ApiName"]);
        Assert.IsType<double>(values["DurationMs"]);
        Assert.DoesNotContain(_logger.ReceivedCalls(), c => c.GetArguments() is [LogLevel.Warning, ..]);
    }

    [Fact]
    public async Task SubmitMission_PostsExactRackMoveBody()
    {
        var (client, handler) = Create(() => Json(Ok));

        await client.SubmitMissionAsync("req1", "mc1", "R1", "Labak-CHTN-33", "Labak-CHTN-25", _ct);

        Assert.Equal("https://kukafm.test/interfaces/api/amr/submitMission", handler.Request!.RequestUri!.ToString());
        Assert.Equal(Token, Assert.Single(handler.Request.Headers.GetValues("Authorization")));
        Assert.Equal(
            """{"orgId":"Labak","requestId":"req1","missionCode":"mc1","missionType":"RACK_MOVE","robotIds":["R1"],"missionData":[""" +
            """{"sequence":1,"position":"Labak-CHTN-33","type":"NODE_POINT","actionType":"UP_CONTAINER","passStrategy":"AUTO","waitingMillis":0},""" +
            """{"sequence":2,"position":"Labak-CHTN-25","type":"NODE_POINT","actionType":"DOWN_CONTAINER","passStrategy":"AUTO","waitingMillis":0}]}""",
            handler.Body);
    }

    [Fact]
    public async Task SubmitMission_SuccessFalse_ThrowsWithKukaCodeAndMessage()
    {
        var (client, _) = Create(() => Json(Failed));

        var ex = await Assert.ThrowsAsync<KukaException>(() => client.SubmitMissionAsync("req1", "mc1", "R1", "A", "B", _ct));

        Assert.Equal(("100409", "mission code duplicate"), (ex.Code, ex.Message));
    }

    [Fact]
    public async Task SubmitMission_Timeout_Throws()
    {
        var (client, _) = Create(_ => throw new TaskCanceledException("timeout", new TimeoutException()));

        var ex = await Assert.ThrowsAsync<KukaException>(() => client.SubmitMissionAsync("req1", "mc1", "R1", "A", "B", _ct));

        Assert.Equal("TIMEOUT", ex.Code);
    }

    [Fact]
    public async Task MissionCancel_PostsForceCancelBody()
    {
        var (client, handler) = Create(() => Json(Ok));

        await client.MissionCancelAsync("req2", "mc1", _ct);

        Assert.Equal("https://kukafm.test/interfaces/api/amr/missionCancel", handler.Request!.RequestUri!.ToString());
        Assert.Equal("""{"requestId":"req2","missionCode":"mc1","containerCode":"","position":"","cancelMode":"FORCE","reason":""}""", handler.Body);
    }

    [Fact]
    public async Task MissionCancel_SuccessFalse_Throws()
    {
        var (client, _) = Create(() => Json("""{"data":null,"code":"100002","message":"invalid parameter","success":false}"""));

        var ex = await Assert.ThrowsAsync<KukaException>(() => client.MissionCancelAsync("req2", "mc1", _ct));

        Assert.Equal("100002", ex.Code);
    }

    [Fact]
    public async Task MissionCancel_Http500_Throws()
    {
        var (client, _) = Create(() => Json("oops", HttpStatusCode.InternalServerError));

        var ex = await Assert.ThrowsAsync<KukaException>(() => client.MissionCancelAsync("req2", "mc1", _ct));

        Assert.Equal("HTTP_500", ex.Code);
    }

    [Fact]
    public async Task JobQuery_EmptyFilters_SendsOnlyLimit()
    {
        var (client, handler) = Create(_ => Task.FromResult(Json(Ok)),
            new JobQueryOptions { Limit = 100, SourceValue = null, CreateUsername = "  ", Maps = [] });

        await client.JobQueryAsync(_ct);

        Assert.Equal("https://kukafm.test/interfaces/api/amr/jobQuery", handler.Request!.RequestUri!.ToString());
        Assert.Equal("""{"limit":100}""", handler.Body);
    }

    [Fact]
    public async Task JobQuery_AllFiltersSet_SendsThemWithLimit()
    {
        var (client, handler) = Create(_ => Task.FromResult(Json(Ok)),
            new JobQueryOptions { Limit = 50, SourceValue = 2, CreateUsername = "middleware", Maps = ["M1", "M2"] });

        await client.JobQueryAsync(_ct);

        Assert.Equal("""{"limit":50,"sourceValue":2,"createUsername":"middleware","maps":["M1","M2"]}""", handler.Body);
    }

    [Fact]
    public async Task JobQuery_Success_MapsJobs()
    {
        var (client, _) = Create(() => Json("""
            {"data":[{"jobCode":"mc1","status":20,"robotId":"R1","workflowId":5,"targetCellCode":"X","completeTime":null}],
             "code":"0","message":null,"success":true}
            """));

        var job = Assert.Single(await client.JobQueryAsync(_ct));

        Assert.Equal(new KukaJob("mc1", 20, "R1"), job);
    }

    [Theory]
    [InlineData("""{"data":[],"code":"0","message":null,"success":true}""")]
    [InlineData("""{"data":null,"code":"0","message":null,"success":true}""")]
    public async Task JobQuery_EmptyData_ReturnsEmptyList(string json)
    {
        var (client, _) = Create(() => Json(json));

        Assert.Empty(await client.JobQueryAsync(_ct));
    }

    [Fact]
    public async Task JobQuery_SuccessFalse_Throws()
    {
        var (client, _) = Create(() => Json("""{"data":null,"code":"100001","message":"unexpected","success":false}"""));

        var ex = await Assert.ThrowsAsync<KukaException>(() => client.JobQueryAsync(_ct));

        Assert.Equal("100001", ex.Code);
    }

    [Fact]
    public async Task JobQueryByCode_SendsOnlyJobCodeWithoutLimitOrFilters()
    {
        var (client, handler) = Create(_ => Task.FromResult(Json(Ok)),
            new JobQueryOptions { Limit = 100, SourceValue = 2, CreateUsername = "middleware", Maps = ["M1"] });

        await client.JobQueryByCodeAsync("mc1", _ct);

        Assert.Equal("https://kukafm.test/interfaces/api/amr/jobQuery", handler.Request!.RequestUri!.ToString());
        Assert.Equal("""{"jobCode":"mc1"}""", handler.Body);
    }

    [Fact]
    public async Task JobQueryByCode_TransportError_Throws()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("connection refused"));

        var ex = await Assert.ThrowsAsync<KukaException>(() => client.JobQueryByCodeAsync("mc1", _ct));

        Assert.Equal("HTTP_ERROR", ex.Code);
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
