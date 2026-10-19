using KukaFm.Middleware.Api.Robots;

namespace KukaFm.Middleware.Api.Tests.Robots;

public class RobotStatusTests
{
    [Theory]
    [InlineData(1, "Departure")]
    [InlineData(2, "Offline")]
    [InlineData(3, "Idle")]
    [InlineData(4, "Executing")]
    [InlineData(5, "Charging")]
    [InlineData(6, "Updating")]
    [InlineData(7, "Abnormal")]
    public void Name_MapsEveryKukaCode(short status, string expected) =>
        Assert.Equal(expected, RobotStatus.Name(status));

    [Theory]
    [InlineData(1, RobotStatus.Busy)]
    [InlineData(2, RobotStatus.Offline)]
    [InlineData(3, RobotStatus.Available)]
    [InlineData(4, RobotStatus.Busy)]
    [InlineData(5, RobotStatus.Busy)]
    [InlineData(6, RobotStatus.Busy)]
    [InlineData(7, RobotStatus.Offline)]
    public void State_MapsEveryKukaCode(short status, string expected) =>
        Assert.Equal(expected, RobotStatus.State(status));
}
