using KukaFm.Middleware.Api.Data;
using KukaFm.Middleware.Api.Kuka;
using KukaFm.Middleware.Api.Sync;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace KukaFm.Middleware.Api.Tests.Sync;

public class MissionSyncServiceTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Before = Created.AddMinutes(1);
    private static readonly DateTimeOffset Now = Created.AddMinutes(2);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly IKukaClient _kuka = Substitute.For<IKukaClient>();
    private readonly IMissionRepository _missions = Substitute.For<IMissionRepository>();
    private readonly IRobotRepository _robots = Substitute.For<IRobotRepository>();
    private readonly SyncReadiness _readiness = new();
    private readonly MissionSyncService _service;

    public MissionSyncServiceTests()
    {
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        _service = new MissionSyncService(_kuka, _missions, _robots, _readiness, time, NullLogger<MissionSyncService>.Instance);
        _readiness.MarkRobotsReady();
        _robots.ListAsync(Arg.Any<CancellationToken>()).Returns([
            new Robot { Id = 1, RobotCode = "R1" },
            new Robot { Id = 2, RobotCode = "R2" },
        ]);
        _kuka.JobQueryByCodeAsync(default!, default).ReturnsForAnyArgs([]);
    }

    private static Mission Stored(string code = "mc1", short status = 20, long robotId = 1) => new()
    {
        Id = 5, MissionCode = code, RequestId = "req-" + code, MissionType = "RACK_MOVE", StartNode = "A", TargetNode = "B",
        RobotId = robotId, Status = status, StatusName = "previous-name", CreatedAt = Created, UpdatedAt = Before,
    };

    private void Given(IReadOnlyList<KukaJob> jobs, params Mission[] active)
    {
        _missions.ListActiveAsync(Arg.Any<CancellationToken>()).Returns(active.ToList());
        _kuka.JobQueryAsync(Arg.Any<CancellationToken>()).Returns(jobs);
    }

    private async Task AssertTickFailed()
    {
        await _missions.DidNotReceiveWithAnyArgs().SaveChangesAsync(_ct);
        Assert.False(_readiness.IsReady);
    }

    [Fact]
    public async Task NoActiveMissions_MakesNoKukaCallAndMarksReady()
    {
        Given([]);

        await _service.SyncAsync(_ct);

        await _kuka.DidNotReceiveWithAnyArgs().JobQueryAsync(_ct);
        await _kuka.DidNotReceiveWithAnyArgs().JobQueryByCodeAsync(default!, _ct);
        Assert.True(_readiness.IsReady);
    }

    [Fact]
    public async Task UnchangedMission_WritesNoHistoryAndKeepsUpdatedAt()
    {
        var mission = Stored();
        Given([new KukaJob("mc1", 20, "R1")], mission);

        await _service.SyncAsync(_ct);

        _missions.DidNotReceiveWithAnyArgs().AddHistory(default!);
        Assert.Equal((Before, "previous-name"), (mission.UpdatedAt, mission.StatusName));
        await _kuka.DidNotReceiveWithAnyArgs().JobQueryByCodeAsync(default!, _ct);
        Assert.True(_readiness.IsReady);
    }

    [Fact]
    public async Task StatusChange_HistoryMirrorsAllPreviousValuesThenMissionIsUpdated()
    {
        var mission = Stored(status: 10);
        MissionStatusHistory? history = null;
        _missions.AddHistory(Arg.Do<MissionStatusHistory>(h => history = h));
        Given([new KukaJob("mc1", 20, "R1")], mission);

        await _service.SyncAsync(_ct);

        Assert.NotNull(history);
        Assert.Equal((5L, "mc1", "req-mc1", "RACK_MOVE", "A", "B", 1L, (short)10, "previous-name", Created, (DateTimeOffset?)null, Before, Now),
            (history.MissionId, history.MissionCode, history.RequestId, history.MissionType, history.StartNode, history.TargetNode,
             history.RobotId, history.Status, history.StatusName, history.CreatedAt, history.CompletedAt, history.ValidFrom, history.ValidTo));
        Assert.Equal(((short)20, "Executing", Now, (DateTimeOffset?)null), (mission.Status, mission.StatusName, mission.UpdatedAt, mission.CompletedAt));
        await _missions.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(30, "Complete")]
    [InlineData(31, "Cancelled")]
    [InlineData(35, "Manual complete")]
    [InlineData(60, "Startup error")]
    public async Task TransitionToFinalStatus_SetsCompletedAtToSyncTime(short status, string name)
    {
        var mission = Stored(status: 28);
        Given([new KukaJob("mc1", status, "R1")], mission);

        await _service.SyncAsync(_ct);

        _missions.Received(1).AddHistory(Arg.Is<MissionStatusHistory>(h => h.Status == 28 && h.CompletedAt == null));
        Assert.Equal((status, name, (DateTimeOffset?)Now), (mission.Status, mission.StatusName, mission.CompletedAt));
    }

    [Theory]
    [InlineData(25)]
    [InlineData(28)]
    [InlineData(50)]
    public async Task TransitionToActiveStatus_LeavesCompletedAtEmpty(short status)
    {
        var mission = Stored(status: 20);
        Given([new KukaJob("mc1", status, "R1")], mission);

        await _service.SyncAsync(_ct);

        Assert.Equal(status, mission.Status);
        Assert.Null(mission.CompletedAt);
    }

    [Fact]
    public async Task RobotChange_WritesHistoryWithPreviousRobotAndUpdatesRobot()
    {
        var mission = Stored(robotId: 1);
        Given([new KukaJob("mc1", 20, "R2")], mission);

        await _service.SyncAsync(_ct);

        _missions.Received(1).AddHistory(Arg.Is<MissionStatusHistory>(h => h.RobotId == 1));
        Assert.Equal((2L, Now), (mission.RobotId, mission.UpdatedAt));
    }

    [Fact]
    public async Task UnknownJobCode_IsIgnored()
    {
        var mission = Stored();
        Given([new KukaJob("mc1", 20, "R1"), new KukaJob("foreign", 30, "UNKNOWN")], mission);

        await _service.SyncAsync(_ct);

        _missions.DidNotReceiveWithAnyArgs().AddHistory(default!);
        await _missions.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.True(_readiness.IsReady);
    }

    [Fact]
    public async Task MissionOutsideLimitWindow_IsQueriedByJobCodeOnlyForMissingMissions()
    {
        var inWindow = Stored("mc1");
        var outside = Stored("mc2");
        Given([new KukaJob("mc1", 20, "R1")], inWindow, outside);
        _kuka.JobQueryByCodeAsync("mc2", Arg.Any<CancellationToken>()).Returns([new KukaJob("mc2", 30, "R1")]);

        await _service.SyncAsync(_ct);

        await _kuka.Received(1).JobQueryByCodeAsync("mc2", Arg.Any<CancellationToken>());
        await _kuka.DidNotReceive().JobQueryByCodeAsync("mc1", Arg.Any<CancellationToken>());
        Assert.Equal(((short)30, (DateTimeOffset?)Now), (outside.Status, outside.CompletedAt));
    }

    [Fact]
    public async Task FallbackReturnsNothing_LeavesMissionUnchangedAndTickSucceeds()
    {
        var changed = Stored("mc1", status: 10);
        var missing = Stored("mc2");
        Given([new KukaJob("mc1", 20, "R1")], changed, missing);

        await _service.SyncAsync(_ct);

        Assert.Equal(((short)20, (short)20, Before), (changed.Status, missing.Status, missing.UpdatedAt));
        _missions.Received(1).AddHistory(Arg.Is<MissionStatusHistory>(h => h.MissionCode == "mc1"));
        await _missions.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.True(_readiness.IsReady);
    }

    [Fact]
    public async Task FallbackReturnsOtherJobCode_IsTreatedAsNotFound()
    {
        var missing = Stored("mc2");
        Given([], missing);
        _kuka.JobQueryByCodeAsync("mc2", Arg.Any<CancellationToken>()).Returns([new KukaJob("other", 30, "R1")]);

        await _service.SyncAsync(_ct);

        Assert.Equal((short)20, missing.Status);
        _missions.DidNotReceiveWithAnyArgs().AddHistory(default!);
    }

    [Fact]
    public async Task FallbackFails_WholeTickFailsAndNothingIsWritten()
    {
        var changed = Stored("mc1", status: 10);
        Given([new KukaJob("mc1", 20, "R1")], changed, Stored("mc2"));
        _kuka.JobQueryByCodeAsync("mc2", Arg.Any<CancellationToken>()).ThrowsAsync(new KukaException("TIMEOUT", "timeout"));

        await _service.SyncAsync(_ct);

        _missions.DidNotReceiveWithAnyArgs().AddHistory(default!);
        await AssertTickFailed();
    }

    [Fact]
    public async Task JobQueryFails_NothingIsWrittenAndNoFallback()
    {
        _missions.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([Stored()]);
        _kuka.JobQueryAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new KukaException("100001", "unexpected"));

        await _service.SyncAsync(_ct);

        await _kuka.DidNotReceiveWithAnyArgs().JobQueryByCodeAsync(default!, _ct);
        await AssertTickFailed();
    }

    [Fact]
    public async Task RobotNotInDatabase_WholeTickFails()
    {
        Given([new KukaJob("mc1", 20, "R2"), new KukaJob("mc2", 30, "R9")], Stored("mc1", status: 10), Stored("mc2"));

        await _service.SyncAsync(_ct);

        await AssertTickFailed();
    }

    [Fact]
    public async Task FallbackJobWithRobotNotInDatabase_WholeTickFails()
    {
        var changed = Stored("mc1", status: 10);
        var missing = Stored("mc2");
        Given([new KukaJob("mc1", 20, "R1")], changed, missing);
        _kuka.JobQueryByCodeAsync("mc2", Arg.Any<CancellationToken>()).Returns([new KukaJob("mc2", 30, "R9")]);

        await _service.SyncAsync(_ct);

        await _kuka.Received(1).JobQueryByCodeAsync("mc2", Arg.Any<CancellationToken>());
        await AssertTickFailed();
    }

    [Fact]
    public async Task LaterFailedTick_KeepsReadinessOpen()
    {
        Given([new KukaJob("mc1", 20, "R1")], Stored());
        await _service.SyncAsync(_ct);
        Assert.True(_readiness.IsReady);

        _kuka.JobQueryAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new KukaException("TIMEOUT", "timeout"));
        await _service.SyncAsync(_ct);

        await _kuka.Received(2).JobQueryAsync(Arg.Any<CancellationToken>());
        Assert.True(_readiness.IsReady);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task NoRobotReported_KeepsCurrentRobotAndCausesNoChange(string? robotCode)
    {
        var mission = Stored(robotId: 2);
        Given([new KukaJob("mc1", 20, robotCode)], mission);

        await _service.SyncAsync(_ct);

        _missions.DidNotReceiveWithAnyArgs().AddHistory(default!);
        Assert.Equal((2L, Before), (mission.RobotId, mission.UpdatedAt));
        Assert.True(_readiness.IsReady);
    }

    [Fact]
    public async Task NoRobotReportedWithStatusChange_KeepsCurrentRobot()
    {
        var mission = Stored(status: 10, robotId: 2);
        Given([new KukaJob("mc1", 20, null)], mission);

        await _service.SyncAsync(_ct);

        _missions.Received(1).AddHistory(Arg.Is<MissionStatusHistory>(h => h.RobotId == 2 && h.Status == 10));
        Assert.Equal(((short)20, 2L), (mission.Status, mission.RobotId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(40)]
    [InlineData(32767 + 10)]
    public async Task StatusOutsideKnownCodes_WholeTickFails(int status)
    {
        Given([new KukaJob("mc1", status, "R1")], Stored());

        await _service.SyncAsync(_ct);

        await AssertTickFailed();
    }

    [Fact]
    public async Task DbErrorOnLoad_MakesNoKukaCallAndWritesNothing()
    {
        _missions.ListActiveAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("db down"));

        await _service.SyncAsync(_ct);

        await _kuka.DidNotReceiveWithAnyArgs().JobQueryAsync(_ct);
        await AssertTickFailed();
    }

    [Fact]
    public async Task DbErrorOnSave_IsSwallowedAndNotReady()
    {
        Given([new KukaJob("mc1", 30, "R1")], Stored());
        _missions.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("db down"));

        var ex = await Record.ExceptionAsync(() => _service.SyncAsync(_ct));

        Assert.Null(ex);
        Assert.False(_readiness.IsReady);
    }

    [Fact]
    public async Task Cancellation_IsPropagated()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _missions.ListActiveAsync(cts.Token).ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.SyncAsync(cts.Token));
    }
}
