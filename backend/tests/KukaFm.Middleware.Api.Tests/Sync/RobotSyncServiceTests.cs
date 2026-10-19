using KukaFm.Middleware.Api.Data;
using KukaFm.Middleware.Api.Kuka;
using KukaFm.Middleware.Api.Sync;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace KukaFm.Middleware.Api.Tests.Sync;

public class RobotSyncServiceTests
{
    private static readonly DateTimeOffset Before = new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = Before.AddMinutes(1);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly IKukaClient _kuka = Substitute.For<IKukaClient>();
    private readonly IRobotRepository _robots = Substitute.For<IRobotRepository>();
    private readonly RobotSyncService _service;

    public RobotSyncServiceTests()
    {
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        _service = new RobotSyncService(_kuka, _robots, time, NullLogger<RobotSyncService>.Instance);
    }

    private static Robot Stored(string code = "R1") => new()
    {
        Id = 7, RobotCode = code, RobotType = "LIFT", Status = 3, StatusName = "Idle", BatteryLevel = 80m,
        MapCode = "M1", NodeCode = "N1", MissionCode = "MC1", UpdatedAt = Before,
    };

    private static KukaRobot Same(string code = "R1") => new(code, "LIFT", 3, 80m, "M1", "N1", "MC1");

    private void Given(IReadOnlyList<KukaRobot> fromKuka, params Robot[] stored)
    {
        _kuka.RobotQueryAsync(Arg.Any<CancellationToken>()).Returns(fromKuka);
        _robots.ListAsync(Arg.Any<CancellationToken>()).Returns(stored.ToList());
    }

    [Fact]
    public async Task NewRobot_IsInsertedWithMappedStatusNameAndNoHistory()
    {
        Given([new KukaRobot("R9", "LIFT", 4, 55.5m, "M1", "N3", "MC9")]);

        await _service.SyncAsync(_ct);

        _robots.Received(1).Add(Arg.Is<Robot>(r =>
            r.RobotCode == "R9" && r.RobotType == "LIFT" && r.Status == 4 && r.StatusName == "Executing"
            && r.BatteryLevel == 55.5m && r.MapCode == "M1" && r.NodeCode == "N3" && r.MissionCode == "MC9" && r.UpdatedAt == Now));
        _robots.DidNotReceiveWithAnyArgs().AddHistory(default!);
        await _robots.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnchangedRobot_WritesNoHistoryAndKeepsUpdatedAt()
    {
        var robot = Stored();
        Given([Same()], robot);

        await _service.SyncAsync(_ct);

        _robots.DidNotReceiveWithAnyArgs().Add(default!);
        _robots.DidNotReceiveWithAnyArgs().AddHistory(default!);
        Assert.Equal(Before, robot.UpdatedAt);
    }

    [Theory]
    [InlineData("RobotType")]
    [InlineData("Status")]
    [InlineData("BatteryLevel")]
    [InlineData("MapCode")]
    [InlineData("NodeCode")]
    [InlineData("MissionCode")]
    [InlineData("RobotTypeToNull")]
    [InlineData("BatteryLevelToNull")]
    [InlineData("MissionCodeToNull")]
    public async Task SingleColumnChange_WritesOneHistoryRowAndUpdatesRobot(string change)
    {
        var robot = Stored();
        var k = change switch
        {
            "RobotType" => Same() with { RobotType = "FORK" },
            "Status" => Same() with { Status = 5 },
            "BatteryLevel" => Same() with { BatteryLevel = 79.99m },
            "MapCode" => Same() with { MapCode = "M2" },
            "NodeCode" => Same() with { NodeCode = "N2" },
            "MissionCode" => Same() with { MissionCode = "MC2" },
            "RobotTypeToNull" => Same() with { RobotType = null },
            "BatteryLevelToNull" => Same() with { BatteryLevel = null },
            "MissionCodeToNull" => Same() with { MissionCode = null },
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };
        Given([k], robot);

        await _service.SyncAsync(_ct);

        _robots.Received(1).AddHistory(Arg.Any<RobotStatusHistory>());
        Assert.Equal((k.RobotType, k.Status, k.BatteryLevel, k.MapCode, k.NodeCode, k.MissionCode, Now),
            (robot.RobotType, robot.Status, robot.BatteryLevel, robot.MapCode, robot.NodeCode, robot.MissionCode, robot.UpdatedAt));
    }

    [Theory]
    [InlineData("RobotType")]
    [InlineData("BatteryLevel")]
    [InlineData("MapCode")]
    [InlineData("NodeCode")]
    [InlineData("MissionCode")]
    public async Task NullToValueChange_WritesHistoryAndUpdatesRobot(string column)
    {
        var robot = new Robot { Id = 7, RobotCode = "R1", Status = 3, StatusName = "Idle", UpdatedAt = Before };
        var empty = new KukaRobot("R1", null, 3, null, null, null, null);
        var k = column switch
        {
            "RobotType" => empty with { RobotType = "LIFT" },
            "BatteryLevel" => empty with { BatteryLevel = 0m },
            "MapCode" => empty with { MapCode = "M1" },
            "NodeCode" => empty with { NodeCode = "N1" },
            "MissionCode" => empty with { MissionCode = "MC1" },
            _ => throw new ArgumentOutOfRangeException(nameof(column)),
        };
        Given([k], robot);

        await _service.SyncAsync(_ct);

        _robots.Received(1).AddHistory(Arg.Any<RobotStatusHistory>());
        Assert.Equal((k.RobotType, k.BatteryLevel, k.MapCode, k.NodeCode, k.MissionCode, Now),
            (robot.RobotType, robot.BatteryLevel, robot.MapCode, robot.NodeCode, robot.MissionCode, robot.UpdatedAt));
    }

    [Fact]
    public async Task Change_HistoryMirrorsAllPreviousValuesWithValidityPeriod()
    {
        var robot = Stored();
        RobotStatusHistory? history = null;
        _robots.AddHistory(Arg.Do<RobotStatusHistory>(h => history = h));
        Given([new KukaRobot("R1", "FORK", 4, 50m, "M2", "N2", "MC2")], robot);

        await _service.SyncAsync(_ct);

        Assert.NotNull(history);
        Assert.Equal((7L, "R1", "LIFT", (short)3, "Idle", 80m, "M1", "N1", "MC1", Before, Now),
            (history.RobotId, history.RobotCode, history.RobotType, history.Status, history.StatusName, history.BatteryLevel,
             history.MapCode, history.NodeCode, history.MissionCode, history.ValidFrom, history.ValidTo));
        Assert.Equal("Executing", robot.StatusName);
    }

    [Fact]
    public async Task MixedBatch_InsertsNewUpdatesChangedAndLeavesMissingRobotUntouched()
    {
        var changed = Stored("R1");
        var unchanged = Stored("R2");
        var missing = Stored("R3");
        Given([Same("R1") with { Status = 2 }, Same("R2"), Same("R4")], changed, unchanged, missing);

        await _service.SyncAsync(_ct);

        _robots.Received(1).Add(Arg.Is<Robot>(r => r.RobotCode == "R4"));
        _robots.Received(1).AddHistory(Arg.Is<RobotStatusHistory>(h => h.RobotCode == "R1"));
        Assert.Equal(Now, changed.UpdatedAt);
        Assert.Equal(Before, unchanged.UpdatedAt);
        Assert.Equal(Before, missing.UpdatedAt);
        Assert.Equal((short)3, missing.Status);
        await _robots.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmptyResponse_LeavesStoredRobotsUnchanged()
    {
        var robot = Stored();
        Given([], robot);

        await _service.SyncAsync(_ct);

        _robots.DidNotReceiveWithAnyArgs().Add(default!);
        _robots.DidNotReceiveWithAnyArgs().AddHistory(default!);
        Assert.Equal(Before, robot.UpdatedAt);
    }

    [Fact]
    public async Task KukaError_IsSwallowedAndNothingIsWritten()
    {
        _kuka.RobotQueryAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new KukaException("100001", "unexpected"));

        await _service.SyncAsync(_ct);

        await _robots.DidNotReceiveWithAnyArgs().ListAsync(_ct);
        _robots.DidNotReceiveWithAnyArgs().Add(default!);
        _robots.DidNotReceiveWithAnyArgs().AddHistory(default!);
        await _robots.DidNotReceiveWithAnyArgs().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task DbErrorOnLoad_IsSwallowedAndNothingIsWritten()
    {
        _kuka.RobotQueryAsync(Arg.Any<CancellationToken>()).Returns([Same()]);
        _robots.ListAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("db down"));

        await _service.SyncAsync(_ct);

        await _robots.DidNotReceiveWithAnyArgs().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task DbErrorOnSave_IsSwallowed()
    {
        Given([Same("R9")]);
        _robots.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("db down"));

        var ex = await Record.ExceptionAsync(() => _service.SyncAsync(_ct));

        Assert.Null(ex);
    }

    [Fact]
    public async Task Cancellation_IsPropagated()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _kuka.RobotQueryAsync(cts.Token).ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.SyncAsync(cts.Token));
    }
}
