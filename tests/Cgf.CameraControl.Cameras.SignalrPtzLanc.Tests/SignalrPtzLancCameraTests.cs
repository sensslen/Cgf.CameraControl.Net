using Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Logger;
using NSubstitute;

namespace Cgf.CameraControl.Cameras.SignalrPtzLanc.Tests;

public class SignalrPtzLancCameraTests
{
    private readonly FakeSignalrStateTransport _transport = new();
    private readonly ILogger _logger = Substitute.For<ILogger>();

    public class Movement : SignalrPtzLancCameraTests
    {
        [Theory]
        [InlineData(1.0, 255)]
        [InlineData(-1.0, -255)]
        [InlineData(0.5, 128)]
        [InlineData(0.0, 0)]
        public void PanIsLinearOverTheFullByteRange(double input, int expected)
        {
            var camera = Connected();

            camera.Pan(input);

            Assert.Equal(expected, _transport.Sent[^1].Pan);
        }

        [Fact]
        public void MovementBeyondFullScaleIsClamped()
        {
            var camera = Connected();

            camera.Pan(4);

            Assert.Equal(255, _transport.Sent[^1].Pan);
        }

        [Fact]
        public void ZoomRunsOverTheEightSpeedsTheLancDriveHas()
        {
            var camera = Connected();

            camera.Zoom(-1);

            Assert.Equal(-8, _transport.Sent[^1].Zoom);
        }

        // The drive has one focus speed each way, reached before the stick is at its stop.
        [Theory]
        [InlineData(1.0, 1)]
        [InlineData(-1.0, -1)]
        [InlineData(0.9, 1)]
        [InlineData(0.2, 0)]
        public void FocusIsADirectionRatherThanASpeed(double input, int expected)
        {
            var camera = Connected();

            camera.Focus(input);

            Assert.Equal(expected, _transport.Sent[^1].Focus);
        }

        [Fact]
        public void PanTiltInvertFlipsMovementButNotTheLens()
        {
            var camera = Connected(panTiltInvert: true);

            camera.Pan(1);
            _transport.Accept();
            camera.Tilt(1);
            _transport.Accept();
            camera.Zoom(1);

            Assert.Equal(-255, _transport.Sent[^3].Pan);
            Assert.Equal(-255, _transport.Sent[^2].Tilt);
            Assert.Equal(8, _transport.Sent[^1].Zoom);
        }

        [Fact]
        public void EveryAxisTravelsInEachUpdate()
        {
            var camera = Connected();
            camera.Pan(1);
            _transport.Accept();

            camera.Tilt(-1);

            Assert.Equal(255, _transport.Sent[^1].Pan);
            Assert.Equal(-255, _transport.Sent[^1].Tilt);
        }

        // This controller drives the camera's own LANC line and has no lamp to light.
        [Fact]
        public void TallyChangesNothing()
        {
            var camera = Connected();

            camera.SetTally(TallyState.Program);

            Assert.Empty(_transport.Sent);
        }
    }

    public class TransmissionGate : SignalrPtzLancCameraTests
    {
        [Fact]
        public void NothingIsSentBeforeTheHubIsUp()
        {
            var camera = Build();

            camera.Pan(1);

            Assert.Empty(_transport.Sent);
        }

        [Fact]
        public void WhatWasAskedForWhileTheHubWasDownGoesOutOnceItIsUp()
        {
            var camera = Build();
            camera.Pan(1);

            _transport.Connect();

            Assert.Single(_transport.Sent);
        }

        [Fact]
        public void OnlyOneCallIsInFlightAtATime()
        {
            var camera = Connected();

            camera.Pan(1);
            camera.Tilt(1);
            camera.Zoom(1);

            Assert.Single(_transport.Sent);
        }

        // The stick has moved on by the time the controller answers, so the position it reached is
        // what goes out, not the one it passed through.
        [Fact]
        public void TheLatestStateIsSentOnceTheCallReturns()
        {
            var camera = Connected();
            camera.Pan(1);
            camera.Pan(0.5);
            camera.Pan(0.25);

            _transport.Accept();

            Assert.Equal(2, _transport.Sent.Count);
            Assert.Equal(64, _transport.Sent[^1].Pan);
        }

        [Fact]
        public void NothingFurtherIsSentWhileTheStateIsUnchanged()
        {
            var camera = Connected();
            camera.Pan(1);

            _transport.Accept();

            Assert.Single(_transport.Sent);
        }

        [Fact]
        public void ARejectedUpdateIsOfferedAgain()
        {
            var camera = Connected();
            camera.Pan(1);

            _transport.Reject();

            Assert.Equal(2, _transport.Sent.Count);
            Assert.Equal(255, _transport.Sent[^1].Pan);
        }

        [Fact]
        public void AFailedCallIsReportedAndTheStateOfferedAgain()
        {
            var camera = Connected();
            camera.Pan(1);

            _transport.Fail("the hub went away");

            _logger.Received().Log(Arg.Is<string>(message => message.Contains("the hub went away")));
            Assert.Equal(2, _transport.Sent.Count);
        }

        [Fact]
        public void LosingTheHubStopsTheCallsRatherThanSpinningOnThem()
        {
            var camera = Connected();
            camera.Pan(1);

            _transport.Disconnect();
            _transport.Reject();

            Assert.Single(_transport.Sent);
        }

        [Fact]
        public void AReconnectResumesFromWhatWasLastAskedFor()
        {
            var camera = Connected();
            camera.Pan(1);
            _transport.Disconnect();
            _transport.Reject();

            _transport.Connect();

            Assert.Equal(2, _transport.Sent.Count);
            Assert.Equal(255, _transport.Sent[^1].Pan);
        }
    }

    public class Identity : SignalrPtzLancCameraTests
    {
        [Fact]
        public void TheConnectionStringNamesTheController()
        {
            Assert.Equal("http://10.0.0.5:5000", Build().ConnectionString);
        }

        [Fact]
        public void TheHubIsOpenedAsSoonAsTheCameraIsBuilt()
        {
            Build();

            Assert.True(_transport.Started);
        }
    }

    private SignalrPtzLancCamera Build(bool panTiltInvert = false) =>
        new(
            new SignalrPtzLancCameraConfiguration
            {
                ConnectionUrl = "http://10.0.0.5:5000",
                ConnectionPort = "COM6",
                PanTiltInvert = panTiltInvert,
            },
            _transport,
            _logger);

    private SignalrPtzLancCamera Connected(bool panTiltInvert = false)
    {
        var camera = Build(panTiltInvert);
        _transport.Connect();
        return camera;
    }
}
