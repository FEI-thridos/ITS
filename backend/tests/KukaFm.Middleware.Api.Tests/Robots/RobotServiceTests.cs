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

        var result = await _service.ListAsync(false, _ct);

        Assert.Equal([
            new RobotDto(2, "A1", "LIFT", 3, "Idle", RobotStatus.Available, 80m, "M", "N", "MC", T1),
            new RobotDto(1, "B1", null, 7, null, RobotStatus.Offline, null, null, null, null, T2),
        ], result);
    }

    [Fact]
    public async Task List_WithoutHistory_DoesNotQueryHistory()
    {
        _robots.ListAsync(Arg.Any<CancellationToken>()).Returns([new Robot { Id = 1, RobotCode = "R1", Status = 3, UpdatedAt = T1 }]);

        var result = await _service.ListAsync(false, _ct);

        Assert.Null(Assert.Single(result).History);
        await _robots.DidNotReceiveWithAnyArgs().ListHistoryAsync(default!, default, _ct);
    }

    [Fact]
    public async Task List_NoRobots_ReturnsEmpty()
    {
        _robots.ListAsync(Arg.Any<CancellationToken>()).Returns([]);

        Assert.Empty(await _service.ListAsync(true, _ct));
    }

    [Fact]
    public async Task List_WithHistory_AttachesNewest100RowsPerRobotNewestFirst()
    {
        _robots.ListAsync(Arg.Any<CancellationToken>()).Returns([
            new Robot { Id = 1, RobotCode = "R1", Status = 3, UpdatedAt = T3 },
            new Robot { Id = 2, RobotCode = "R2", Status = 3, UpdatedAt = T3 },
        ]);
        _robots.ListHistoryAsync(Arg.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 1, 2 })), 100, Arg.Any<CancellationToken>())
            .Returns([
                new RobotStatusHistory { Id = 10, RobotId = 1, RobotCode = "R1", Status = 2, ValidFrom = T1, ValidTo = T2 },
                new RobotStatusHistory { Id = 11, RobotId = 1, RobotCode = "R1", Status = 4, ValidFrom = T2, ValidTo = T3 },
            ]);

        var result = await _service.ListAsync(true, _ct);

        Assert.Equal([(short)4, (short)2], result[0].History!.Select(h => h.Status));
        Assert.Equal([RobotStatus.Busy, RobotStatus.Offline], result[0].History!.Select(h => h.State));
        Assert.Empty(result[1].History!);
    }

    [Fact]
    public async Task History_UnknownRobot_ReturnsNullWithoutQueryingHistory()
    {
        _robots.GetAsync(99, Arg.Any<CancellationToken>()).Returns((Robot?)null);

        Assert.Null(await _service.ListHistoryAsync(99, 1, 100, _ct));
        await _robots.DidNotReceiveWithAnyArgs().ListHistoryPageAsync(default, default, default, _ct);
    }

    [Fact]
    public async Task History_ExistingRobot_ReturnsRequestedPageWithTotalCount()
    {
        _robots.GetAsync(1, Arg.Any<CancellationToken>()).Returns(new Robot { Id = 1, RobotCode = "R1" });
        _robots.ListHistoryPageAsync(1, 3, 2, Arg.Any<CancellationToken>()).Returns(([
            new RobotStatusHistory { RobotId = 1, RobotCode = "R1", RobotType = "LIFT", Status = 4, StatusName = "Executing", BatteryLevel = 70m, MapCode = "M", NodeCode = "N2", MissionCode = "MC", ValidFrom = T2, ValidTo = T3 },
            new RobotStatusHistory { RobotId = 1, RobotCode = "R1", Status = 3, StatusName = "Idle", ValidFrom = T1, ValidTo = T2 },
        ], 234));

        var result = await _service.ListHistoryAsync(1, 3, 2, _ct);

        Assert.NotNull(result);
        Assert.Equal((3, 2, 234), (result.Page, result.PageSize, result.TotalCount));
        Assert.Equal([
            new RobotHistoryDto("R1", "LIFT", 4, "Executing", RobotStatus.Busy, 70m, "M", "N2", "MC", T2, T3),
            new RobotHistoryDto("R1", null, 3, "Idle", RobotStatus.Available, null, null, null, null, T1, T2),
        ], result.Items);
    }

    [Fact]
    public async Task History_PageBeyondEnd_ReturnsEmptyItemsWithTotalCount()
    {
        _robots.GetAsync(1, Arg.Any<CancellationToken>()).Returns(new Robot { Id = 1, RobotCode = "R1" });
        _robots.ListHistoryPageAsync(1, 9, 100, Arg.Any<CancellationToken>()).Returns(([], 5));

        var result = await _service.ListHistoryAsync(1, 9, 100, _ct);

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(5, result.TotalCount);
    }
}
