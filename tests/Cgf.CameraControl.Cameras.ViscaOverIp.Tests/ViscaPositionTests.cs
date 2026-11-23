using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Logger;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Cgf.CameraControl.Cameras.ViscaOverIp.Tests;

public class ViscaPositionTests : IAsyncDisposable
{
    private readonly FakeViscaTransport _transport = new();
    private readonly FakeTimeProvider _time = new();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly ViscaOverIpCamera _camera;

    public ViscaPositionTests()
    {
        _camera = new(new() { Ip = "127.0.0.1", PanTiltSpeedPercent = 50 }, _transport, _logger, _time);
        _transport.Connect();
    }

    public ValueTask DisposeAsync() => _camera.DisposeAsync();

    public class Packets
    {
        [Fact]
        public void AbsolutePanTiltWritesSignedPositionsAsNibbles()
        {
            Assert.Equal<byte[]>(
                [0x81, 0x01, 0x06, 0x02, 0x0C, 0x0A, 0x0F, 0x0F, 0x0F, 0x06, 0x00, 0x01, 0x02, 0x0C, 0xFF],
                ViscaPacket.PanTiltAbsolute(12, 10, new PanTiltPosition(-10, 300)));
        }

        [Fact]
        public void DirectLensPositionsAreFourNibbles()
        {
            Assert.Equal<byte[]>([0x81, 0x01, 0x04, 0x47, 0x04, 0x00, 0x00, 0x00, 0xFF], ViscaPacket.ZoomDirect(0x4000));
            Assert.Equal<byte[]>([0x81, 0x01, 0x04, 0x48, 0x01, 0x02, 0x03, 0x04, 0xFF], ViscaPacket.FocusDirect(0x1234));
        }

        [Fact]
        public void ReadsANegativePanBack()
        {
            Assert.True(ViscaPacket.TryReadPanTilt(
                [0x90, 0x50, 0x0F, 0x0F, 0x0F, 0x06, 0x00, 0x01, 0x02, 0x0C, 0xFF],
                out var position));
            Assert.Equal(new PanTiltPosition(-10, 300), position);
        }

        [Fact]
        public void RejectsBytesThatAreNotNibbles()
        {
            Assert.False(ViscaPacket.TryReadLensPosition([0x90, 0x50, 0x10, 0x00, 0x00, 0x00, 0xFF], out _));
            Assert.False(ViscaPacket.TryReadPanTilt([0x90, 0x50, 0x00, 0x00, 0x00, 0x00, 0xFF], out _));
        }
    }

    public class Reading : ViscaPositionTests
    {
        [Fact]
        public async Task ReadsAllThreePartsOneInquiryAtATime()
        {
            var reading = _camera.ReadPositionAsync(TestContext.Current.CancellationToken);

            Assert.Equal(ViscaPacket.PanTiltPositionInquiry(), Assert.Single(_transport.Sent));
            _transport.Receive(0x90, 0x50, 0x00, 0x00, 0x01, 0x00, 0x0F, 0x0F, 0x0F, 0x0F, 0xFF);
            Assert.Equal(ViscaPacket.ZoomPositionInquiry(), _transport.Sent[^1]);
            _transport.Receive(0x90, 0x50, 0x01, 0x00, 0x00, 0x00, 0xFF);
            Assert.Equal(ViscaPacket.FocusPositionInquiry(), _transport.Sent[^1]);
            _transport.Receive(0x90, 0x50, 0x00, 0x02, 0x00, 0x00, 0xFF);

            Assert.Equal(
                new CameraPosition(new PanTiltPosition(16, -1), 0x1000, 0x0200),
                await reading.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }

        /// The probe: a part the camera refuses comes back null rather than failing the read.
        [Fact]
        public async Task ARefusedPartIsReportedAsUnsupported()
        {
            var reading = _camera.ReadPositionAsync(TestContext.Current.CancellationToken);

            _transport.Receive(0x90, 0x50, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF);
            _transport.Refuse(socket: 0, error: 0x02);
            _transport.Receive(0x90, 0x50, 0x00, 0x00, 0x00, 0x00, 0xFF);

            var position = await reading.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.NotNull(position.PanTilt);
            Assert.Null(position.Zoom);
            Assert.Equal(0, position.Focus);
        }

        [Fact]
        public async Task ACameraThatNeverAnswersReportsNothingSupported()
        {
            var reading = _camera.ReadPositionAsync(TestContext.Current.CancellationToken);

            for (var inquiry = 0; inquiry < 3; inquiry++)
            {
                _time.Advance(TimeSpan.FromSeconds(5));
            }

            Assert.True((await reading.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).IsEmpty);
        }

        [Fact]
        public async Task ADisconnectedCameraAnswersAtOnce()
        {
            _transport.Disconnect();

            Assert.True((await _camera.ReadPositionAsync(TestContext.Current.CancellationToken)).IsEmpty);
            Assert.Empty(_transport.Sent);
        }

        [Fact]
        public async Task ALostConnectionEndsTheReadWithWhatArrived()
        {
            var reading = _camera.ReadPositionAsync(TestContext.Current.CancellationToken);
            _transport.Receive(0x90, 0x50, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0xFF);

            _transport.Disconnect();

            var position = await reading.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(new PanTiltPosition(1, 2), position.PanTilt);
            Assert.Null(position.Zoom);
        }
    }

    public class Recalling : ViscaPositionTests
    {
        private static readonly CameraPosition Stored = new(new PanTiltPosition(100, -20), 0x2000, 0x0300);

        [Fact]
        public void MovesOnePartAtATimeAndFocusInManualFocus()
        {
            _camera.MoveTo(Stored);

            Assert.Equal(ViscaPacket.PanTiltAbsolute(12, 10, new PanTiltPosition(100, -20)), Assert.Single(_transport.Sent));
            _transport.Acknowledge();
            Assert.Single(_transport.Sent);
            _transport.Complete();
            Assert.Equal(ViscaPacket.ZoomDirect(0x2000), _transport.Sent[^1]);
            _transport.Acknowledge();
            _transport.Complete();
            Assert.Equal(ViscaPacket.ManualFocus(), _transport.Sent[^1]);
            _transport.Acknowledge();
            _transport.Complete();
            Assert.Equal(ViscaPacket.FocusDirect(0x0300), _transport.Sent[^1]);
        }

        [Fact]
        public void MovesOnlyThePartsTheSceneStores()
        {
            _camera.MoveTo(new CameraPosition(null, 0x1000, null));
            _transport.Acknowledge();
            _transport.Complete();

            Assert.Equal(ViscaPacket.ZoomDirect(0x1000), Assert.Single(_transport.Sent));
        }

        /// The sticks keep the other buffer while a stored-position move runs.
        [Fact]
        public void TheSticksStillDriveDuringAMove()
        {
            _camera.MoveTo(Stored);
            _transport.Acknowledge();

            _camera.Tilt(1);

            Assert.Equal(ViscaPacket.PanTilt(0, 10), _transport.Sent[^1]);
        }

        [Fact]
        public void SteeringAnAxisDropsItsQueuedMove()
        {
            _camera.MoveTo(Stored);
            _transport.Acknowledge();
            _camera.Zoom(1);
            _transport.Acknowledge(socket: 2);
            _transport.Complete(socket: 2);

            _transport.Complete();

            Assert.DoesNotContain(_transport.Sent, packet => packet.AsSpan().SequenceEqual(ViscaPacket.ZoomDirect(0x2000)));
            Assert.Equal(ViscaPacket.ManualFocus(), _transport.Sent[^1]);
        }

        /// Releasing the sticks after a recall must not interrupt it: the stop it sends is the level
        /// the camera already holds, and that is not sent again.
        [Fact]
        public void LettingGoAfterARecallDoesNotStopIt()
        {
            _camera.Pan(0);
            _transport.Acknowledge();
            _transport.Complete();
            var before = _transport.Sent.Count;
            _camera.MoveTo(new CameraPosition(new PanTiltPosition(1, 1), null, null));
            _transport.Acknowledge();

            _camera.Pan(0);

            Assert.Equal(before + 1, _transport.Sent.Count);
        }

        [Fact]
        public void RecallingTheSameSceneAgainMovesAgain()
        {
            var target = new CameraPosition(null, 0x1000, null);
            _camera.MoveTo(target);
            _transport.Acknowledge();
            _transport.Complete();

            _camera.MoveTo(target);

            Assert.Equal(2, _transport.Sent.Count);
        }

        [Fact]
        public void AMoveThatNeverCompletesStopsHoldingBackTheNext()
        {
            _camera.MoveTo(Stored);
            _transport.Acknowledge();

            _time.Advance(TimeSpan.FromSeconds(30));

            Assert.Equal(ViscaPacket.ZoomDirect(0x2000), _transport.Sent[^1]);
            _logger.Received().Error("ViscaOverIpCamera", Arg.Is<string>(message =>
                message.Contains("no completion for the pan and tilt position command")));
        }

        [Fact]
        public void ALateCompletionForAnExpiredMoveDoesNotReleaseTheNext()
        {
            _camera.MoveTo(Stored);
            _transport.Acknowledge();
            _time.Advance(TimeSpan.FromSeconds(30));
            Assert.Equal(ViscaPacket.ZoomDirect(0x2000), _transport.Sent[^1]);

            _transport.Complete();
            _camera.Tilt(1);

            Assert.Equal(ViscaPacket.ZoomDirect(0x2000), _transport.Sent[^1]);
            _transport.Acknowledge(socket: 2);
            Assert.Equal(ViscaPacket.PanTilt(0, 10), _transport.Sent[^1]);
        }

        [Fact]
        public void ARefusedManualFocusGoesOutAgainBeforeTheFocusTarget()
        {
            _camera.MoveTo(new CameraPosition(null, null, 0x0300));
            Assert.Equal(ViscaPacket.ManualFocus(), Assert.Single(_transport.Sent));

            _transport.Refuse(socket: 0);

            Assert.Equal(2, _transport.Sent.Count(packet => packet.AsSpan().SequenceEqual(ViscaPacket.ManualFocus())));
            Assert.Equal(ViscaPacket.ManualFocus(), _transport.Sent[^1]);
            _transport.Acknowledge();
            _transport.Complete();
            Assert.Equal(ViscaPacket.FocusDirect(0x0300), _transport.Sent[^1]);
        }

        [Fact]
        public void ACameraThatRefusesManualFocusIsNotSentTheFocusTarget()
        {
            _camera.MoveTo(new CameraPosition(null, null, 0x0300));

            _transport.Refuse(socket: 0, error: 0x41);

            Assert.DoesNotContain(_transport.Sent, packet => packet.AsSpan().SequenceEqual(ViscaPacket.FocusDirect(0x0300)));
        }

        [Fact]
        public void ALateRefusalForAnExpiredMoveDoesNotReleaseTheNext()
        {
            _camera.MoveTo(Stored);
            _transport.Acknowledge();
            _time.Advance(TimeSpan.FromSeconds(30));
            Assert.Equal(ViscaPacket.ZoomDirect(0x2000), _transport.Sent[^1]);

            _transport.Refuse(socket: 1, error: 0x41);
            _camera.Tilt(1);

            Assert.Equal(ViscaPacket.ZoomDirect(0x2000), _transport.Sent[^1]);
        }

        [Fact]
        public void ALateRefusalForAnExpiredMoveKeepsTheFocusTarget()
        {
            _camera.MoveTo(Stored);
            _transport.Acknowledge();
            _time.Advance(TimeSpan.FromSeconds(30));
            _transport.Acknowledge(socket: 2);
            _transport.Complete(socket: 2);
            Assert.Equal(ViscaPacket.ManualFocus(), _transport.Sent[^1]);

            _transport.Refuse(socket: 1, error: 0x41);
            _transport.Acknowledge(socket: 2);
            _transport.Complete(socket: 2);

            Assert.Equal(ViscaPacket.FocusDirect(0x0300), _transport.Sent[^1]);
        }

        [Fact]
        public void ALostConnectionDropsTheRestOfTheRecall()
        {
            _camera.MoveTo(Stored);
            _transport.Disconnect();
            var sent = _transport.Sent.Count;

            _transport.Connect();

            Assert.Equal(sent, _transport.Sent.Count);
        }
    }
}
