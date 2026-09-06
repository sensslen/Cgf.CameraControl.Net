using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Logger;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Cgf.CameraControl.Cameras.ViscaOverIp.Tests;

public class ViscaOverIpCameraTests
{
    private readonly FakeViscaTransport _transport = new();
    private readonly FakeTimeProvider _time = new();
    private readonly ILogger _logger = Substitute.For<ILogger>();

    public class Movement : ViscaOverIpCameraTests
    {
        [Theory]
        [InlineData(1.0, 0x18, 0x02)]
        [InlineData(-1.0, 0x18, 0x01)]
        [InlineData(0.5, 0x0C, 0x02)]
        [InlineData(0.0, 0x01, 0x03)]
        public void PanScalesTheStickOntoTheCameraSpeedRange(double input, byte speed, byte direction)
        {
            var camera = Connected();

            camera.Pan(input);

            Assert.Equal(speed, _transport.Sent[^1][4]);
            Assert.Equal(direction, _transport.Sent[^1][6]);
        }

        // A stick held on the diagonal has to move the camera on the diagonal.
        [Fact]
        public void PanAndTiltTravelTogether()
        {
            var camera = Connected();
            camera.Pan(1);
            _transport.Acknowledge();

            camera.Tilt(1);

            Assert.Equal<byte[]>([0x81, 0x01, 0x06, 0x01, 0x18, 0x14, 0x02, 0x01, 0xFF], _transport.Sent[^1]);
        }

        [Fact]
        public void PanTiltInvertFlipsMovementButNotTheLens()
        {
            var camera = Connected(panTiltInvert: true);

            camera.Pan(1);
            _transport.Acknowledge();
            camera.Tilt(1);
            _transport.Acknowledge();
            camera.Zoom(1);

            Assert.Equal(0x01, _transport.Sent[^3][6]);
            Assert.Equal(0x02, _transport.Sent[^2][7]);
            Assert.Equal(0x27, _transport.Sent[^1][4]);
        }

        // The lens has a speed zero and the axes do not, so a stick just off centre has to reach the
        // slowest zoom the camera owns rather than the second slowest.
        [Theory]
        [InlineData(0.1, 0x20)]
        [InlineData(0.5, 0x23)]
        [InlineData(1.0, 0x27)]
        [InlineData(-1.0, 0x37)]
        [InlineData(0.0, 0x00)]
        public void ZoomScalesTheStickOntoTheWholeLensSpeedRange(double input, byte expected)
        {
            var camera = Connected();

            camera.Zoom(input);

            Assert.Equal(expected, _transport.Sent[^1][4]);
        }

        [Fact]
        public void ZoomAndFocusAreSeparateOperations()
        {
            var camera = Connected();
            camera.Zoom(1);
            _transport.Acknowledge();

            camera.Focus(-1);

            Assert.Equal(0x07, _transport.Sent[^2][3]);
            Assert.Equal(0x08, _transport.Sent[^1][3]);
        }
    }

    public class CommandGate : ViscaOverIpCameraTests
    {
        [Fact]
        public void NothingIsSentBeforeTheSocketIsUp()
        {
            var camera = Build();

            camera.Pan(1);

            Assert.Empty(_transport.Sent);
        }

        [Fact]
        public void WhatWasAskedForWhileTheSocketWasDownGoesOutOnceItIsUp()
        {
            var camera = Build();
            camera.Pan(1);

            _transport.Connect();

            Assert.Single(_transport.Sent);
        }

        [Fact]
        public void OnlyOneCommandIsInFlightAtATime()
        {
            var camera = Connected();

            camera.Pan(1);
            camera.Zoom(1);
            camera.Focus(1);

            Assert.Single(_transport.Sent);
        }

        [Fact]
        public void AnAcknowledgementLetsTheNextCommandOut()
        {
            var camera = Connected();
            camera.Pan(1);
            camera.Zoom(1);

            _transport.Acknowledge();

            Assert.Equal(2, _transport.Sent.Count);
        }

        // The stick has moved on by the time the camera answers, so the position it reached is what
        // goes out, not the one it passed through.
        [Fact]
        public void OnlyTheLatestPositionOfAnAxisSurvivesTheWait()
        {
            var camera = Connected();
            camera.Pan(1);
            camera.Pan(0.5);
            camera.Pan(0.25);

            _transport.Acknowledge();

            Assert.Equal(2, _transport.Sent.Count);
            Assert.Equal(0x06, _transport.Sent[^1][4]);
        }

        [Fact]
        public void CommandsOfDifferentKindsKeepTheOrderTheyWereAskedFor()
        {
            var camera = Connected();
            camera.Pan(1);
            camera.Focus(1);
            camera.Zoom(1);

            _transport.Acknowledge();
            _transport.Acknowledge();

            Assert.Equal(0x08, _transport.Sent[1][3]);
            Assert.Equal(0x07, _transport.Sent[2][3]);
        }

        [Fact]
        public void ARefusalAlsoLetsTheNextCommandOut()
        {
            var camera = Connected();
            camera.Pan(1);
            camera.Zoom(1);

            _transport.Refuse();

            Assert.Equal(2, _transport.Sent.Count);
        }

        // A refusal says what went wrong but never which command it went wrong for, and the answer
        // arrives after the request is gone. A camera in auto focus refuses every manual focus
        // command, and a log that does not name the command leaves that undiagnosable.
        [Fact]
        public void ARefusalNamesTheCommandItRefused()
        {
            var camera = Connected();
            camera.Focus(1);

            _transport.Refuse();

            _logger.Received().Error(Arg.Any<string>(), Arg.Is<string>(message =>
                message.Contains("the focus command") && message.Contains("command buffer full")));
        }

        [Fact]
        public void ARefusalOfOneAxisDoesNotNameAnother()
        {
            var camera = Connected();
            camera.Pan(1);

            _transport.Refuse();

            _logger.Received().Error(Arg.Any<string>(), Arg.Is<string>(message => message.Contains("the pan and tilt command")));
        }

        // A camera that stops answering must not wedge the gate shut for the rest of the service.
        [Fact]
        public void ACommandThatIsNeverAnsweredExpires()
        {
            var camera = Connected();
            camera.Pan(1);
            camera.Zoom(1);

            _time.Advance(TimeSpan.FromSeconds(5));

            Assert.Equal(2, _transport.Sent.Count);
        }

        // A camera acknowledges into a buffer and reports the outcome against that buffer, which can
        // be several commands later. Naming whatever is in flight then would blame the wrong axis.
        [Fact]
        public void ARefusalAfterAnAcknowledgementNamesTheCommandInThatBuffer()
        {
            var camera = Connected();
            camera.Focus(1);
            _transport.Acknowledge(socket: 2);
            camera.Pan(1);

            _transport.Refuse(socket: 2);

            _logger.Received().Error(Arg.Any<string>(), Arg.Is<string>(message => message.Contains("the focus command")));
        }

        [Fact]
        public void ACommandThatRanLeavesNothingToBlameLater()
        {
            var camera = Connected();
            camera.Focus(1);
            _transport.Acknowledge(socket: 2);
            _transport.Complete(socket: 2);
            camera.Pan(1);

            _transport.Refuse(socket: 2);

            _logger.DidNotReceive().Error(Arg.Any<string>(), Arg.Is<string>(message => message.Contains("the focus command")));
        }

        [Fact]
        public void AnExpiryNamesTheCommandThatWasNeverAnswered()
        {
            var camera = Connected();
            camera.Zoom(1);

            _time.Advance(TimeSpan.FromSeconds(5));

            _logger.Received().Error(Arg.Any<string>(), Arg.Is<string>(message => message.Contains("the zoom command")));
        }

        [Fact]
        public void AnAnsweredCommandDoesNotExpireLater()
        {
            var camera = Connected();
            camera.Pan(1);
            _transport.Acknowledge();

            _time.Advance(TimeSpan.FromSeconds(30));

            _logger.DidNotReceive().Error(Arg.Any<string>(), Arg.Any<string>());
        }

        // A drive command is a level the camera holds, so repeating it asks for nothing. Interfaces
        // stop every axis of a camera they let go of, and a camera in auto focus refuses a focus
        // stop, which would otherwise be one refusal in the log per selection change.
        [Fact]
        public void SendingTheSameCommandAgainAsksForNothing()
        {
            var camera = Connected();
            camera.Focus(0);
            _transport.Acknowledge();

            camera.Focus(0);

            Assert.Single(_transport.Sent);
        }

        [Fact]
        public void AChangedCommandStillGoesOut()
        {
            var camera = Connected();
            camera.Pan(1);
            _transport.Acknowledge();

            camera.Pan(0.5);

            Assert.Equal(2, _transport.Sent.Count);
        }

        // Whatever it was told before is forgotten by a camera that restarted.
        [Fact]
        public void AReconnectSendsTheCurrentPositionAgain()
        {
            var camera = Connected();
            camera.Pan(1);
            _transport.Acknowledge();
            _transport.Disconnect();
            _transport.Connect();

            camera.Pan(1);

            Assert.Equal(2, _transport.Sent.Count);
        }

        [Fact]
        public void AReplyToNothingIsIgnored()
        {
            var camera = Connected();

            _transport.Receive(0x90, 0x38, 0xFF);
            camera.Pan(1);

            Assert.Single(_transport.Sent);
        }

        // An in flight command will never be answered now, so the gate has to open by itself.
        [Fact]
        public void LosingTheCameraDoesNotWedgeTheGateShut()
        {
            var camera = Connected();
            camera.Pan(1);
            camera.Zoom(1);

            _transport.Disconnect();
            _transport.Connect();

            Assert.Equal(2, _transport.Sent.Count);
        }
    }

    public class Tally : ViscaOverIpCameraTests
    {
        [Fact]
        public void ACameraWithNoNamedVendorIsToldNothing()
        {
            var camera = Connected();

            camera.SetTally(TallyState.Program);

            Assert.Empty(_transport.Sent);
        }

        // Red on the seventh byte and green on the eighth, which is what lets one packet say
        // program, preview or dark.
        [Theory]
        [InlineData(TallyState.Program, (byte)0x02, (byte)0x03)]
        [InlineData(TallyState.Preview, (byte)0x03, (byte)0x02)]
        [InlineData(TallyState.Off, (byte)0x03, (byte)0x03)]
        public void AvonicCarriesBothLampsInOnePayload(TallyState state, byte red, byte green)
        {
            var camera = Connected(tally: ViscaTallyMode.Avonic);

            camera.SetTally(state);

            Assert.Equal<byte[]>([0x81, 0x01, 0x7E, 0x01, 0x0A, 0x00, red, green, 0xFF], _transport.Sent[^1]);
        }

        // This one has a lamp rather than a colour, so preview leaves it dark.
        [Fact]
        public void PtzOpticsLightsUpOnlyOnProgram()
        {
            var camera = Connected(tally: ViscaTallyMode.PtzOptics);

            camera.SetTally(TallyState.Program);
            _transport.Acknowledge();
            camera.SetTally(TallyState.Preview);

            Assert.Equal(0x02, _transport.Sent[^2][^2]);
            Assert.Equal(0x03, _transport.Sent[^1][^2]);
        }

        [Fact]
        public void TallyDoesNotDisplaceMovement()
        {
            var camera = Connected(tally: ViscaTallyMode.Avonic);
            camera.Pan(1);
            camera.SetTally(TallyState.Program);

            _transport.Acknowledge();

            Assert.Equal(0x7E, _transport.Sent[^1][2]);
        }
    }

    public class Identity : ViscaOverIpCameraTests
    {
        [Fact]
        public void TheConnectionStringNamesTheSocketTheCameraIsOn()
        {
            Assert.Equal("visca://10.0.0.1:52381", Build().ConnectionString);
        }

        [Fact]
        public void TheSocketIsOpenedAsSoonAsTheCameraIsBuilt()
        {
            Build();

            Assert.True(_transport.Started);
        }
    }

    private ViscaOverIpCamera Build(bool panTiltInvert = false, ViscaTallyMode tally = ViscaTallyMode.None) =>
        new(
            new ViscaOverIpCameraConfiguration
            {
                Ip = "10.0.0.1",
                PanTiltInvert = panTiltInvert,
                TallyMode = tally,
            },
            _transport,
            _logger,
            _time);

    private ViscaOverIpCamera Connected(bool panTiltInvert = false, ViscaTallyMode tally = ViscaTallyMode.None)
    {
        var camera = Build(panTiltInvert, tally);
        _transport.Connect();
        return camera;
    }
}
