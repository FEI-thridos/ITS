using KukaFm.Middleware.Api.Data;
using KukaFm.Middleware.Api.Kuka;
using KukaFm.Middleware.Api.Missions;
using KukaFm.Middleware.Api.Sync;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace KukaFm.Middleware.Api.Tests.Missions;

public class MissionServiceTests
{
    private static readonly DateTimeOffset T1 = new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = T1.AddMinutes(5);
    private static readonly DateTimeOffset Now = T1.AddHours(1);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly IMissionRepository _missions = Substitute.For<IMissionRepository>();
    private readonly IRobotRepository _robots = Substitute.For<IRobotRepository>();
    private readonly IKukaClient _kuka = Substitute.For<IKukaClient>();
    private readonly SyncReadiness _readiness = new();
    private readonly MissionService _service;

    public MissionServiceTests()
    {
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        _service = new MissionService(_missions, _robots, _kuka, _readiness, time, NullLogger<MissionService>.Instance);
        _readiness.MarkRobotsReady();
        _readiness.MarkMissionsReady();
        _robots.GetAsync(1, Arg.Any<CancellationToken>()).Returns(new Robot { Id = 1, RobotCode = "R1" });
    }

    private static DispatchMissionRequest Valid => new(1, "Labak-CHTN-33", "Labak-CHTN-25");

    private static Mission StoredMission(short status) => new()
    {
        Id = 5, MissionCode = "mc1", RequestId = "req1", MissionType = "RACK_MOVE", StartNode = "A", TargetNode = "B",
        RobotId = 1, Robot = new Robot { Id = 1, RobotCode = "R1" }, Status = status, StatusName = MissionStatus.Name(status),
        CreatedAt = T1, UpdatedAt = T2,
    };

    private async Task AssertNoKukaCall()
    {
        await _kuka.DidNotReceiveWithAnyArgs().SubmitMissionAsync(default!, default!, default!, default!, default!, _ct);
        await _kuka.DidNotReceiveWithAnyArgs().MissionCancelAsync(default!, default!, _ct);
    }

    // --- readiness ---

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Dispatch_BeforeSyncsReady_ReturnsNotReadyWithoutKukaCall(bool robotsReady, bool missionsReady)
    {
        var readiness = new SyncReadiness();
        if (robotsReady) readiness.MarkRobotsReady();
        if (missionsReady) readiness.MarkMissionsReady();
        var service = new MissionService(_missions, _robots, _kuka, readiness, TimeProvider.System, NullLogger<MissionService>.Instance);

        var result = await service.DispatchAsync(Valid, _ct);

        Assert.Equal((MissionOutcome.NotReady, "Synchronization not ready yet"), (result.Outcome, result.Detail));
        await AssertNoKukaCall();
        _missions.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Cancel_BeforeSyncsReady_ReturnsNotReadyWithoutKukaCall(bool robotsReady, bool missionsReady)
    {
        var readiness = new SyncReadiness();
        if (robotsReady) readiness.MarkRobotsReady();
        if (missionsReady) readiness.MarkMissionsReady();
        var service = new MissionService(_missions, _robots, _kuka, readiness, TimeProvider.System, NullLogger<MissionService>.Instance);
        _missions.GetByCodeAsync("mc1", Arg.Any<CancellationToken>()).Returns(StoredMission(20));

        var result = await service.CancelAsync("mc1", _ct);

        Assert.Equal((MissionOutcome.NotReady, "Synchronization not ready yet"), (result.Outcome, result.Detail));
        await AssertNoKukaCall();
    }

    // --- dispatch validation ---

    [Theory]
    [InlineData(null, "startNode")]
    [InlineData("", "startNode")]
    [InlineData("   ", "startNode")]
    [InlineData("\t\n", "startNode")]
    public async Task Dispatch_MissingStartNode_IsInvalidWithoutKukaCall(string? startNode, string field)
    {
        var result = await _service.DispatchAsync(Valid with { StartNode = startNode }, _ct);

        Assert.Equal(MissionOutcome.Invalid, result.Outcome);
        Assert.Equal([field], result.Errors!.Keys);
        await AssertNoKukaCall();
        _missions.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Dispatch_MissingTargetNode_IsInvalidWithoutKukaCall(string? targetNode)
    {
        var result = await _service.DispatchAsync(Valid with { TargetNode = targetNode }, _ct);

        Assert.Equal(MissionOutcome.Invalid, result.Outcome);
        Assert.Equal(["targetNode"], result.Errors!.Keys);
        await AssertNoKukaCall();
    }

    [Theory]
    [InlineData("startNode")]
    [InlineData("targetNode")]
    public async Task Dispatch_65CharNode_IsInvalidWithoutKukaCall(string field)
    {
        var tooLong = new string('N', 65);
        var request = field == "startNode" ? Valid with { StartNode = tooLong } : Valid with { TargetNode = tooLong };

        var result = await _service.DispatchAsync(request, _ct);

        Assert.Equal(MissionOutcome.Invalid, result.Outcome);
        Assert.Equal([field], result.Errors!.Keys);
        await AssertNoKukaCall();
    }

    [Fact]
    public async Task Dispatch_64CharNodesWithSurroundingWhitespace_AreTrimmedAndAccepted()
    {
        var start = new string('S', 64);
        var target = new string('T', 64);

        var result = await _service.DispatchAsync(new DispatchMissionRequest(1, $"  {start} ", $"\t{target}  "), _ct);

        Assert.Equal(MissionOutcome.Success, result.Outcome);
        await _kuka.Received(1).SubmitMissionAsync(Arg.Any<string>(), Arg.Any<string>(), "R1", start, target, Arg.Any<CancellationToken>());
        _missions.Received(1).Add(Arg.Is<Mission>(m => m.StartNode == start && m.TargetNode == target));
    }

    [Fact]
    public async Task Dispatch_UnknownRobot_IsInvalidUnderRobotIdWithoutKukaCall()
    {
        _robots.GetAsync(99, Arg.Any<CancellationToken>()).Returns((Robot?)null);

        var result = await _service.DispatchAsync(Valid with { RobotId = 99 }, _ct);

        Assert.Equal(MissionOutcome.Invalid, result.Outcome);
        Assert.Equal(["robotId"], result.Errors!.Keys);
        await AssertNoKukaCall();
    }

    [Fact]
    public async Task Dispatch_MissingRobotId_IsInvalidWithoutRobotLookup()
    {
        var result = await _service.DispatchAsync(Valid with { RobotId = null }, _ct);

        Assert.Equal(["robotId"], result.Errors!.Keys);
        await _robots.DidNotReceiveWithAnyArgs().GetAsync(default, _ct);
        await AssertNoKukaCall();
    }

    [Fact]
    public async Task Dispatch_AllFieldsInvalid_ReportsEveryFieldCamelCase()
    {
        _robots.GetAsync(99, Arg.Any<CancellationToken>()).Returns((Robot?)null);

        var result = await _service.DispatchAsync(new DispatchMissionRequest(99, " ", new string('x', 65)), _ct);

        Assert.Equal(["robotId", "startNode", "targetNode"], result.Errors!.Keys.Order());
    }

    [Fact]
    public async Task Dispatch_StartEqualsTarget_IsAllowed()
    {
        var result = await _service.DispatchAsync(Valid with { TargetNode = Valid.StartNode }, _ct);

        Assert.Equal(MissionOutcome.Success, result.Outcome);
    }

    // --- dispatch KUKA + storage ---

    [Fact]
    public async Task Dispatch_Success_StoresCreatedMissionWithCodesSentToKuka()
    {
        string? sentRequestId = null, sentMissionCode = null;
        await _kuka.SubmitMissionAsync(
            Arg.Do<string>(r => sentRequestId = r), Arg.Do<string>(m => sentMissionCode = m),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Mission? stored = null;
        _missions.Add(Arg.Do<Mission>(m => stored = m));

        var result = await _service.DispatchAsync(Valid, _ct);

        Assert.NotNull(stored);
        Assert.Equal((sentMissionCode, sentRequestId, "RACK_MOVE", "Labak-CHTN-33", "Labak-CHTN-25", 1L, (short)10, "Created", Now, Now, (DateTimeOffset?)null),
            (stored.MissionCode, stored.RequestId, stored.MissionType, stored.StartNode, stored.TargetNode, stored.RobotId,
             stored.Status, stored.StatusName, stored.CreatedAt, stored.UpdatedAt, stored.CompletedAt));
        await _missions.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.Equal(MissionOutcome.Success, result.Outcome);
        Assert.Equal(new MissionDto(0, sentMissionCode!, "RACK_MOVE", "Labak-CHTN-33", "Labak-CHTN-25", 1, "R1", 10, "Created", true, Now, Now, null),
            result.Mission);
    }

    [Fact]
    public async Task Dispatch_GeneratesDistinct32CharHexCodesPerMission()
    {
        var codes = new List<string>();
        await _kuka.SubmitMissionAsync(
            Arg.Do<string>(codes.Add), Arg.Do<string>(codes.Add),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        await _service.DispatchAsync(Valid, _ct);
        await _service.DispatchAsync(Valid, _ct);

        Assert.Equal(4, codes.Count);
        Assert.Equal(4, codes.Distinct().Count());
        Assert.All(codes, c => Assert.Matches("^[0-9a-f]{32}$", c));
    }

    [Theory]
    [InlineData("100301", "robot not exists")]
    [InlineData("100204", "no such node")]
    [InlineData("TIMEOUT", "Request timed out")]
    public async Task Dispatch_KukaError_ReturnsKukaErrorAndStoresNothing(string code, string message)
    {
        _kuka.SubmitMissionAsync(default!, default!, default!, default!, default!, _ct)
            .ThrowsAsyncForAnyArgs(new KukaException(code, message));

        var result = await _service.DispatchAsync(Valid, _ct);

        Assert.Equal((MissionOutcome.KukaError, $"KUKA FM error {code}: {message}"), (result.Outcome, result.Detail));
        _missions.DidNotReceiveWithAnyArgs().Add(default!);
        await _missions.DidNotReceiveWithAnyArgs().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Dispatch_SaveFailsAfterKukaAccepted_ReturnsNotStoredAndLogsError()
    {
        string? sentRequestId = null, sentMissionCode = null;
        await _kuka.SubmitMissionAsync(
            Arg.Do<string>(r => sentRequestId = r), Arg.Do<string>(m => sentMissionCode = m),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _missions.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("db down"));
        var logger = Substitute.For<ILogger<MissionService>>();
        var time = Substitute.For<TimeProvider>();
        var service = new MissionService(_missions, _robots, _kuka, _readiness, time, logger);

        var result = await service.DispatchAsync(Valid, _ct);

        Assert.Equal((MissionOutcome.NotStored, $"Mission {sentMissionCode} was accepted by KUKA FM but could not be stored; it is not tracked."),
            (result.Outcome, result.Detail));
        Assert.Null(result.Mission);
        await _kuka.DidNotReceiveWithAnyArgs().MissionCancelAsync(default!, default!, _ct);
        var error = Assert.Single(logger.ReceivedCalls(), c => c.GetArguments() is [LogLevel.Error, ..]).GetArguments();
        var values = ((IReadOnlyList<KeyValuePair<string, object?>>)error[2]!).ToDictionary(kv => kv.Key, kv => kv.Value);
        Assert.Equal((sentMissionCode, sentRequestId, "R1", "Labak-CHTN-33", "Labak-CHTN-25"),
            (values["MissionCode"], values["RequestId"], values["RobotCode"], values["StartNode"], values["TargetNode"]));
        Assert.IsType<InvalidOperationException>(error[3]);
    }

    [Fact]
    public async Task Dispatch_RequestCancelledAfterKukaAccepted_StillStoresMission()
    {
        using var cts = new CancellationTokenSource();
        await _kuka.SubmitMissionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Do<CancellationToken>(_ => cts.Cancel()));

        var result = await _service.DispatchAsync(Valid, cts.Token);

        Assert.True(cts.IsCancellationRequested);
        Assert.Equal(MissionOutcome.Success, result.Outcome);
        await _kuka.Received(1).SubmitMissionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), CancellationToken.None);
        _missions.Received(1).Add(Arg.Any<Mission>());
        await _missions.Received(1).SaveChangesAsync(CancellationToken.None);
    }

    // --- cancel ---

    [Fact]
    public async Task Cancel_UnknownMission_ReturnsNotFoundWithoutKukaCall()
    {
        _missions.GetByCodeAsync("nope", Arg.Any<CancellationToken>()).Returns((Mission?)null);

        var result = await _service.CancelAsync("nope", _ct);

        Assert.Equal(MissionOutcome.NotFound, result.Outcome);
        await AssertNoKukaCall();
    }

    [Theory]
    [InlineData(28)]
    [InlineData(30)]
    [InlineData(31)]
    [InlineData(35)]
    [InlineData(60)]
    public async Task Cancel_NotCancelableStatus_ReturnsConflictWithoutKukaCall(short status)
    {
        _missions.GetByCodeAsync("mc1", Arg.Any<CancellationToken>()).Returns(StoredMission(status));

        var result = await _service.CancelAsync("mc1", _ct);

        Assert.Equal(MissionOutcome.NotCancelable, result.Outcome);
        await AssertNoKukaCall();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(25)]
    [InlineData(50)]
    public async Task Cancel_CancelableStatus_SendsForceCancelAndKeepsLocalStatus(short status)
    {
        var mission = StoredMission(status);
        _missions.GetByCodeAsync("mc1", Arg.Any<CancellationToken>()).Returns(mission);

        var result = await _service.CancelAsync("mc1", _ct);

        Assert.Equal(MissionOutcome.Success, result.Outcome);
        await _kuka.Received(1).MissionCancelAsync(Arg.Is<string>(r => r.Length == 32 && r != "req1"), "mc1", Arg.Any<CancellationToken>());
        Assert.Equal((status, T2), (mission.Status, mission.UpdatedAt));
        _missions.DidNotReceiveWithAnyArgs().AddHistory(default!);
        await _missions.DidNotReceiveWithAnyArgs().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Cancel_KukaError_ReturnsKukaErrorAndKeepsLocalStatus()
    {
        var mission = StoredMission(20);
        _missions.GetByCodeAsync("mc1", Arg.Any<CancellationToken>()).Returns(mission);
        _kuka.MissionCancelAsync(default!, default!, _ct).ThrowsAsyncForAnyArgs(new KukaException("100001", "unexpected"));

        var result = await _service.CancelAsync("mc1", _ct);

        Assert.Equal((MissionOutcome.KukaError, "KUKA FM error 100001: unexpected"), (result.Outcome, result.Detail));
        Assert.Equal((short)20, mission.Status);
        await _missions.DidNotReceiveWithAnyArgs().SaveChangesAsync(_ct);
    }

    // --- list ---

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("active", MissionState.Active)]
    [InlineData("finished", MissionState.Finished)]
    public void TryParseState_ValidValues(string? value, MissionState? expected)
    {
        Assert.True(MissionService.TryParseState(value, out var state));
        Assert.Equal(expected, state);
    }

    [Theory]
    [InlineData("all")]
    [InlineData("Active")]
    [InlineData("cancelled")]
    [InlineData(" active")]
    public void TryParseState_InvalidValues(string value) =>
        Assert.False(MissionService.TryParseState(value, out _));

    [Theory]
    [InlineData(null)]
    [InlineData(MissionState.Active)]
    [InlineData(MissionState.Finished)]
    public async Task List_PassesFilterToRepositoryAndMapsDto(MissionState? state)
    {
        var mission = StoredMission(31);
        mission.CompletedAt = Now;
        _missions.ListAsync(state, Arg.Any<CancellationToken>()).Returns([mission]);

        var result = await _service.ListAsync(state, false, _ct);

        Assert.Equal(new MissionDto(5, "mc1", "RACK_MOVE", "A", "B", 1, "R1", 31, "Cancelled", false, T1, T2, Now), Assert.Single(result));
        await _missions.DidNotReceiveWithAnyArgs().ListHistoryAsync(default!, default, _ct);
    }

    [Fact]
    public async Task List_WithHistory_AttachesNewest100RowsPerMissionNewestFirst()
    {
        var other = StoredMission(10);
        other.Id = 6;
        _missions.ListAsync(null, Arg.Any<CancellationToken>()).Returns([StoredMission(20), other]);
        var robot2 = new Robot { Id = 2, RobotCode = "R2" };
        _missions.ListHistoryAsync(Arg.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 5, 6 })), 100, Arg.Any<CancellationToken>())
            .Returns([
                History(1, 10, robot2, T1, T1.AddMinutes(1)),
                History(2, 25, robot2, T1.AddMinutes(1), T2),
            ]);

        var result = await _service.ListAsync(null, true, _ct);

        Assert.Equal([(short)25, (short)10], result[0].History!.Select(h => h.Status));
        Assert.All(result[0].History!, h => Assert.Equal(("R2", 2L), (h.RobotCode, h.RobotId)));
        Assert.Empty(result[1].History!);
    }

    // --- history ---

    [Fact]
    public async Task History_UnknownMission_ReturnsNullWithoutQueryingHistory()
    {
        _missions.GetByCodeAsync("nope", Arg.Any<CancellationToken>()).Returns((Mission?)null);

        Assert.Null(await _service.ListHistoryAsync("nope", 1, 100, _ct));
        await _missions.DidNotReceiveWithAnyArgs().ListHistoryPageAsync(default, default, default, _ct);
    }

    [Fact]
    public async Task History_ExistingMission_ReturnsRequestedPageWithTotalCount()
    {
        _missions.GetByCodeAsync("mc1", Arg.Any<CancellationToken>()).Returns(StoredMission(20));
        var robot = new Robot { Id = 1, RobotCode = "R1" };
        _missions.ListHistoryPageAsync(5, 2, 1, Arg.Any<CancellationToken>()).Returns(([History(7, 10, robot, T1, T2)], 234));

        var result = await _service.ListHistoryAsync("mc1", 2, 1, _ct);

        Assert.NotNull(result);
        Assert.Equal((2, 1, 234), (result.Page, result.PageSize, result.TotalCount));
        Assert.Equal(new MissionHistoryDto("mc1", "RACK_MOVE", "A", "B", 1, "R1", 10, "Created", T1, null, T1, T2), Assert.Single(result.Items));
    }

    private static MissionStatusHistory History(long id, short status, Robot robot, DateTimeOffset from, DateTimeOffset to) => new()
    {
        Id = id, MissionId = 5, MissionCode = "mc1", RequestId = "req1", MissionType = "RACK_MOVE", StartNode = "A", TargetNode = "B",
        RobotId = robot.Id, Robot = robot, Status = status, StatusName = MissionStatus.Name(status), CreatedAt = T1,
        ValidFrom = from, ValidTo = to,
    };
}
