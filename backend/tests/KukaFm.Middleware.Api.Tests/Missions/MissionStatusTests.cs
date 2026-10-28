using KukaFm.Middleware.Api.Missions;

namespace KukaFm.Middleware.Api.Tests.Missions;

public class MissionStatusTests
{
    [Theory]
    [InlineData(10, "Created", true, true, false)]
    [InlineData(20, "Executing", true, true, false)]
    [InlineData(25, "Waiting", true, true, false)]
    [InlineData(28, "Cancelling", true, false, false)]
    [InlineData(50, "Warning", true, true, false)]
    [InlineData(30, "Complete", false, false, true)]
    [InlineData(31, "Cancelled", false, false, true)]
    [InlineData(35, "Manual complete", false, false, true)]
    [InlineData(60, "Startup error", false, false, true)]
    public void EveryKukaCode_HasNameAndClass(short status, string name, bool active, bool cancelable, bool final) =>
        Assert.Equal((name, active, cancelable, final),
            (MissionStatus.Name(status), MissionStatus.IsActive(status), MissionStatus.IsCancelable(status), MissionStatus.IsFinal(status)));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(11)]
    [InlineData(29)]
    [InlineData(40)]
    [InlineData(-10)]
    public void UnknownCode_Throws(int status) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MissionStatus.Name(status));
}
