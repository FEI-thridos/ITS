using KukaFm.Middleware.Api.Data;
using KukaFm.Middleware.Api.Robots;
using NSubstitute;

namespace KukaFm.Middleware.Api.Tests.Robots;

public class RobotServiceTests
{
    private static readonly DateTimeOffset T1 = new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = T1.AddMinutes(5);
    private static readonly DateTimeOffset T3 = T1.AddMinutes(10);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly IRobotRepository _robots = Substitute.For<IRobotRepository>();
    private readonly RobotService _service;

    public RobotServiceTests() => _service = new RobotService(_robots);

    [Fact]
    public async Task List_KeepsRepositoryOrderAndMapsAllFieldsWithState()
    {
        _robots.ListAsync(Arg.Any<CancellationToken>()).Returns([
            new Robot { Id = 2, RobotCode = "A1", RobotType = "LIFT", Status = 3, StatusName = "Idle", BatteryLevel = 80m, MapCode = "M", NodeCode = "N", MissionCode = "MC", UpdatedAt = T1 },
            new Robot { Id = 1, RobotCode = "B1", Status = 7, UpdatedAt = T2 },
        ]);

        var result = await _service.ListAsync(_ct);

        Assert.Equal([
            new RobotDto(2, "A1", "LIFT", 3, "Idle", RobotStatus.Available, 80m, "M", "N", "MC", T1),
            new RobotDto(1, "B1", null, 7, null, RobotStatus.Offline, null, null, null, null, T2),
        ], result);
    }

    [Fact]
    public async Task List_NoRobots_ReturnsEmpty()
    {
        _robots.ListAsync(Arg.Any<CancellationToken>()).Returns([]);

        Assert.Empty(await _service.ListAsync(_ct));
    }

    [Fact]
    public async Task History_UnknownRobot_ReturnsNullWithoutQueryingHistory()
    {
        _robots.ExistsAsync(99, Arg.Any<CancellationToken>()).Returns(false);

        Assert.Null(await _service.ListHistoryAsync(99, _ct));
        await _robots.DidNotReceiveWithAnyArgs().ListHistoryAsync(default, default, _ct);
    }

    [Fact]
    public async Task History_ExistingRobot_ReturnsRowsNewestFirstAsQueriedWithMax50()
    {
        _robots.ExistsAsync(1, Arg.Any<CancellationToken>()).Returns(true);
        _robots.ListHistoryAsync(1, 50, Arg.Any<CancellationToken>()).Returns([
            new RobotStatusHistory { RobotId = 1, RobotCode = "R1", RobotType = "LIFT", Status = 4, StatusName = "Executing", BatteryLevel = 70m, MapCode = "M", NodeCode = "N2", MissionCode = "MC", ValidFrom = T2, ValidTo = T3 },
            new RobotStatusHistory { RobotId = 1, RobotCode = "R1", Status = 3, StatusName = "Idle", ValidFrom = T1, ValidTo = T2 },
        ]);

        var result = await _service.ListHistoryAsync(1, _ct);

        Assert.Equal([
            new RobotHistoryDto("R1", "LIFT", 4, "Executing", RobotStatus.Busy, 70m, "M", "N2", "MC", T2, T3),
            new RobotHistoryDto("R1", null, 3, "Idle", RobotStatus.Available, null, null, null, null, T1, T2),
        ], result);
    }

    [Fact]
    public async Task History_ExistingRobotWithoutHistory_ReturnsEmptyList()
    {
        _robots.ExistsAsync(1, Arg.Any<CancellationToken>()).Returns(true);
        _robots.ListHistoryAsync(1, 50, Arg.Any<CancellationToken>()).Returns([]);

        var result = await _service.ListHistoryAsync(1, _ct);

        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
