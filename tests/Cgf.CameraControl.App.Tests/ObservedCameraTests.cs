using Cgf.CameraControl.App.Hosting;
using Cgf.CameraControl.Core.CameraConnection;
using NSubstitute;

namespace Cgf.CameraControl.App.Tests;

public class ObservedCameraTests
{
    private readonly ICameraConnection _inner = Substitute.For<ICameraConnection>();

    [Fact]
    public void EverythingStillReachesTheCamera()
    {
        var camera = new ObservedCamera(_inner);

        camera.Pan(0.5);
        camera.Tilt(-0.5);
        camera.Zoom(1);
        camera.Focus(-1);
        camera.SetTally(TallyState.Program);

        _inner.Received(1).Pan(0.5);
        _inner.Received(1).Tilt(-0.5);
        _inner.Received(1).Zoom(1);
        _inner.Received(1).Focus(-1);
        _inner.Received(1).SetTally(TallyState.Program);
    }

    [Fact]
    public void EachAxisIsRecordedWithoutClearingTheOthers()
    {
        var camera = new ObservedCamera(_inner);

        camera.Pan(0.5);
        camera.Zoom(-0.25);

        Assert.Equal(new CameraMovement(0.5, 0, -0.25, 0), Latest(camera.WhenMovementChanged));
    }

    [Fact]
    public void TallyIsRecorded()
    {
        var camera = new ObservedCamera(_inner);

        camera.SetTally(TallyState.Preview);

        Assert.Equal(TallyState.Preview, Latest(camera.WhenTallyChanged));
    }

    private static T Latest<T>(IObservable<T> source)
    {
        T? latest = default;
        using (source.Subscribe(value => latest = value))
        {
            return latest!;
        }
    }
}
