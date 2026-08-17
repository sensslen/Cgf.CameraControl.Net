using Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;
using Cgf.CameraControl.Core.CameraConnection;

namespace Cgf.CameraControl.Cameras.WebsocketPtzLanc.Tests;

public class WebsocketPtzLancCameraTests
{
    private readonly FakeWebsocketTransport _transport = new();

    public class Movement : WebsocketPtzLancCameraTests
    {
        [Theory]
        [InlineData(1.0, 255)]
        [InlineData(-1.0, -255)]
        [InlineData(0.5, 64)]
        [InlineData(-0.5, -64)]
        [InlineData(0.0, 0)]
        public void PanSquaresTheStickPositionAndKeepsItsSign(double input, int expected)
        {
            var camera = Opened();

            camera.Pan(input);

            Assert.Equal(expected, _transport.Sent[^1].Pan);
        }

        [Fact]
        public void MovementBeyondFullScaleIsClamped()
        {
            var camera = Opened();

            camera.Pan(4);
            _transport.EchoLastSend();
            camera.Tilt(-4);

            Assert.Equal(255, _transport.Sent[^2].Pan);
            Assert.Equal(-255, _transport.Sent[^1].Tilt);
        }

        // The curve is deliberately not applied to zoom.
        [Fact]
        public void ZoomIsLinear()
        {
            var camera = Opened();

            camera.Zoom(0.5);

            Assert.Equal(128, _transport.Sent[^1].Zoom);
        }

        [Fact]
        public void PanTiltInvertFlipsMovementButNotZoom()
        {
            var camera = Opened(panTiltInvert: true);

            camera.Pan(1);
            _transport.EchoLastSend();
            camera.Tilt(1);
            _transport.EchoLastSend();
            camera.Zoom(1);

            Assert.Equal(-255, _transport.Sent[^3].Pan);
            Assert.Equal(-255, _transport.Sent[^2].Tilt);
            Assert.Equal(255, _transport.Sent[^1].Zoom);
        }

        [Fact]
        public void FocusIsNotSupportedAndChangesNothing()
        {
            var camera = Opened();
            var before = _transport.Sent.Count;

            camera.Focus(1);

            Assert.Equal(before, _transport.Sent.Count);
        }
    }

    public class EchoGate : WebsocketPtzLancCameraTests
    {
        [Fact]
        public void NothingIsSentBeforeTheCameraHasReportedItself()
        {
            var camera = Build();

            camera.Pan(1);

            Assert.Empty(_transport.Sent);
        }

        [Fact]
        public void AnEchoThatDisagreesWithTheRequestResendsIt()
        {
            var camera = Build();
            camera.Pan(1);

            _transport.Echo(default);

            Assert.Equal(255, Assert.Single(_transport.Sent).Pan);
        }

        [Fact]
        public void OnlyOneRequestIsInFlightAtATime()
        {
            var camera = Opened();
            var before = _transport.Sent.Count;

            camera.Pan(1);
            camera.Pan(0.5);
            camera.Pan(0.25);

            Assert.Equal(before + 1, _transport.Sent.Count);
        }

        [Fact]
        public void TheLatestRequestIsSentOnceTheGateOpens()
        {
            var camera = Opened();
            camera.Pan(1);
            camera.Pan(0.5);
            camera.Pan(0.25);

            _transport.EchoLastSend();

            Assert.Equal(16, _transport.Sent[^1].Pan);
        }

        [Fact]
        public void AnUnreadableEchoIsIgnoredRatherThanClosingTheGate()
        {
            var camera = Opened();

            _transport.EchoRaw("not json");
            camera.Pan(1);

            Assert.Equal(255, _transport.Sent[^1].Pan);
        }
    }

    public class Tally : WebsocketPtzLancCameraTests
    {
        [Theory]
        [InlineData(TallyState.Preview, 0, 255)]
        [InlineData(TallyState.Program, 255, 0)]
        [InlineData(TallyState.Off, 0, 0)]
        public void TallyDrivesTheIndicatorColours(TallyState state, int red, int green)
        {
            var camera = Opened();
            camera.SetTally(TallyState.Program);
            _transport.EchoLastSend();

            camera.SetTally(state);

            Assert.Equal(red, _transport.Sent[^1].Red);
            Assert.Equal(green, _transport.Sent[^1].Green);
        }

        [Fact]
        public void TallyIsIgnoredWhenTheCameraDoesNotDriveALight()
        {
            var camera = Opened(showTallyLight: false);
            var before = _transport.Sent.Count;

            camera.SetTally(TallyState.Program);

            Assert.Equal(before, _transport.Sent.Count);
        }

        // Every camera in ensi.json sets showTallyLight false, so a controller reporting its own
        // colours must not keep the gate shut forever.
        [Fact]
        public void AnEchoDifferingOnlyInTallyStillOpensTheGate()
        {
            var camera = Opened(showTallyLight: false);
            camera.Pan(1);

            _transport.Echo(_transport.Sent[^1] with { Red = 255, Green = 128 });
            camera.Pan(0.5);

            Assert.Equal(64, _transport.Sent[^1].Pan);
        }
    }

    private WebsocketPtzLancCamera Build(bool panTiltInvert = false, bool showTallyLight = true) =>
        new(
            new WebsocketPtzLancCameraConfiguration
            {
                Ip = "10.0.0.1",
                PanTiltInvert = panTiltInvert,
                ShowTallyLight = showTallyLight,
            },
            _transport);

    /// The controller announces its idle state on connect, which is what opens the gate.
    private WebsocketPtzLancCamera Opened(bool panTiltInvert = false, bool showTallyLight = true)
    {
        var camera = Build(panTiltInvert, showTallyLight);
        _transport.Echo(default);
        return camera;
    }
}
