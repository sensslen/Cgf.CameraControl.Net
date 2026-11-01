using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Logger;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Cgf.CameraControl.Cameras.ViscaOverIp.Tests;

public class ViscaColorTests : IAsyncDisposable
{
    private readonly FakeViscaTransport _transport = new();
    private readonly FakeTimeProvider _time = new();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly ViscaOverIpCamera _camera;
    private CameraColorState? _state;

    public ViscaColorTests()
    {
        _camera = new(new() { Ip = "127.0.0.1" }, _transport, _logger, _time);
        _camera.WhenColorChanged.Subscribe(state => _state = state);
        _transport.Connect();
    }

    public ValueTask DisposeAsync() => _camera.DisposeAsync();

    public class Discovery : ViscaColorTests
    {
        [Fact]
        public void ReadsSettingsWithoutChangingThemAndOnlyEnablesAfterAllReplies()
        {
            Assert.Empty(_transport.Sent);
            _camera.RefreshColor();
            Assert.Equal<byte[]>([0x81, 0x09, 0x04, 0x35, 0xFF], _transport.Sent[^1]);
            _transport.Receive(0x90, 0x50, 0x05, 0xFF);
            Assert.Null(_state);
            Assert.Equal<byte[]>([0x81, 0x09, 0x04, 0x43, 0xFF], _transport.Sent[^1]);
            _transport.Receive(0x90, 0x50, 0x00, 0x00, 0x08, 0x01, 0xFF);
            Assert.Null(_state);
            Assert.Equal<byte[]>([0x81, 0x09, 0x04, 0x44, 0xFF], _transport.Sent[^1]);
            _transport.Receive(0x90, 0x50, 0x00, 0x00, 0x07, 0x0F, 0xFF);
            Assert.Equal(new CameraColorState(true, 129, 127), _state);
            Assert.Equal(3, _transport.Sent.Count);
        }

        [Fact]
        public void UnsupportedInquiryKeepsCorrectionUnavailableAndReleasesMovement()
        {
            _camera.RefreshColor();
            _camera.Pan(1);
            _transport.Refuse(socket: 0, error: 0x02);
            _transport.Receive(0x90, 0x50, 0x00, 0x00, 0x08, 0x00, 0xFF);
            _transport.Receive(0x90, 0x50, 0x00, 0x00, 0x08, 0x00, 0xFF);
            Assert.Null(_state);
            Assert.Equal(ViscaPacket.PanTilt(24, 0), _transport.Sent[^1]);
            _logger.Received().Error(Arg.Any<string>(), Arg.Is<string>(message =>
                message.Contains("white balance inquiry") && message.Contains("syntax error")));
        }

        [Fact]
        public void MissingInquiryTimesOutWithoutRetryingForever()
        {
            _camera.RefreshColor();
            _camera.Zoom(1);
            for (var index = 0; index < 3; index++)
            {
                _time.Advance(TimeSpan.FromSeconds(5));
            }
            Assert.Null(_state);
            Assert.Equal(4, _transport.Sent.Count);
            Assert.Equal(ViscaPacket.Zoom(8), _transport.Sent[^1]);
            _logger.Received().Error(Arg.Any<string>(), Arg.Is<string>(message =>
                message.Contains("no answer to the white balance inquiry")));
        }

        [Fact]
        public void PresenceAndOldAcknowledgementsCannotCompleteAnInquiry()
        {
            _camera.RefreshColor();
            _transport.Receive(0x90, 0x50, 0x00, 0x01, 0x00, 0x02, 0x01, 0x02, 0xFF);
            _transport.Acknowledge();
            _transport.Complete();
            _transport.Complete(socket: 0);
            Assert.Single(_transport.Sent);
            Assert.Null(_state);
        }

        [Fact]
        public void InvalidGainNibblesDoNotAdvertiseSupport()
        {
            _camera.RefreshColor();
            _transport.Receive(0x90, 0x50, 0x05, 0xFF);
            _transport.Receive(0x90, 0x50, 0x00, 0x00, 0x10, 0x00, 0xFF);
            Assert.Equal(2, _transport.Sent.Count);
            Assert.Null(_state);
        }

        [Fact]
        public void RefreshCanReadChangedSettingsAgain()
        {
            Discover();
            _camera.RefreshColor();
            _transport.Receive(0x90, 0x50, 0x00, 0xFF);
            _transport.Receive(0x90, 0x50, 0x00, 0x00, 0x01, 0x00, 0xFF);
            _transport.Receive(0x90, 0x50, 0x00, 0x00, 0x02, 0x00, 0xFF);
            Assert.Equal(new CameraColorState(false, 16, 32), _state);
        }
    }

    public class Correction : ViscaColorTests
    {
        [Fact]
        public void RefusedOnePushPreparationCancelsTriggerButNotMovement()
        {
            Discover();
            _camera.OnePushWhiteBalance();
            _camera.Zoom(0);
            _transport.Refuse(socket: 0, error: 0x41);
            Assert.Equal(5, _transport.Sent.Count);
            Assert.Equal(ViscaPacket.Zoom(0), _transport.Sent[^1]);
            Assert.Null(_state);
            _logger.Received().Error(Arg.Any<string>(), Arg.Is<string>(message =>
                message.Contains("white balance command") && message.Contains("not executable")));
        }

        [Fact]
        public void LostWhiteBalanceActionDoesNotReplayOrBlockMovementForever()
        {
            Discover();
            _camera.OnePushWhiteBalance();
            _camera.SetManualWhiteBalance(true);
            _camera.SetRedGain(200);
            _camera.Zoom(0);
            _time.Advance(TimeSpan.FromSeconds(5));
            Assert.Equal(5, _transport.Sent.Count);
            Assert.Equal(ViscaPacket.Zoom(0), _transport.Sent[^1]);
            Assert.Null(_state);
            _logger.Received().Error(Arg.Any<string>(), Arg.Is<string>(message =>
                message.Contains("no answer to the white balance command")));
        }

        [Fact]
        public void ReadbackInvalidatesGainDeduplicationAfterCameraSettingsChange()
        {
            Discover();
            _camera.SetRedGain(100);
            _transport.Acknowledge();
            Discover();
            _camera.SetRedGain(100);
            Assert.Equal(8, _transport.Sent.Count);
            Assert.Equal(ViscaPacket.RedGain(100), _transport.Sent[^1]);
        }

        [Fact]
        public void OnePushCompletionRefreshesGainsBeforeManualAdjustment()
        {
            Discover();
            _camera.OnePushWhiteBalance();
            _transport.Acknowledge(socket: 1);
            _transport.Acknowledge(socket: 2);
            _transport.Complete(socket: 1);
            Assert.Equal(5, _transport.Sent.Count);
            _transport.Complete(socket: 2);
            Assert.Null(_state);
            Assert.Equal(ViscaPacket.WhiteBalanceInquiry(), _transport.Sent[^1]);
            _transport.Receive(0x90, 0x50, 0x03, 0xFF);
            _transport.Receive(0x90, 0x50, 0x00, 0x00, 0x09, 0x00, 0xFF);
            _transport.Receive(0x90, 0x50, 0x00, 0x00, 0x07, 0x00, 0xFF);
            Assert.Equal(new CameraColorState(false, 144, 112), _state);
        }

        [Fact]
        public void EveryOnePushSendsModeThenTriggerEvenWhenRepeated()
        {
            Discover();
            _camera.OnePushWhiteBalance();
            _transport.Acknowledge();
            _transport.Acknowledge();
            _camera.OnePushWhiteBalance();
            _transport.Acknowledge();
            Assert.Equal(7, _transport.Sent.Count);
            Assert.Equal<byte[]>([0x81, 0x01, 0x04, 0x35, 0x03, 0xFF], _transport.Sent[3]);
            Assert.Equal<byte[]>([0x81, 0x01, 0x04, 0x10, 0x05, 0xFF], _transport.Sent[4]);
            Assert.Equal(_transport.Sent[3], _transport.Sent[5]);
            Assert.Equal(_transport.Sent[4], _transport.Sent[6]);
            Assert.NotNull(_state);
            Assert.False(_state.IsManual);
        }

        [Fact]
        public void ChangingModesCannotCoalesceAwayOnePushPreparation()
        {
            Discover();
            _camera.Pan(1);
            _camera.OnePushWhiteBalance();
            _camera.SetManualWhiteBalance(true);
            _camera.SetRedGain(255);
            for (var index = 0; index < 4; index++)
            {
                _transport.Acknowledge();
            }

            Assert.Equal(ViscaPacket.OnePushWhiteBalanceMode(), _transport.Sent[4]);
            Assert.Equal(ViscaPacket.OnePushWhiteBalanceTrigger(), _transport.Sent[5]);
            Assert.Equal(ViscaPacket.WhiteBalance(true), _transport.Sent[6]);
            Assert.Equal<byte[]>([0x81, 0x01, 0x04, 0x43, 0, 0, 0x0F, 0x0F, 0xFF], _transport.Sent[7]);
        }

        [Fact]
        public void GainSlidersCoalesceAndKeepBothChannelsIndependent()
        {
            Discover();
            _camera.Pan(1);
            _camera.SetRedGain(17);
            _camera.SetRedGain(34);
            _camera.SetBlueGain(255);
            _transport.Acknowledge();
            _transport.Acknowledge();
            Assert.Equal<byte[]>([0x81, 0x01, 0x04, 0x43, 0, 0, 2, 2, 0xFF], _transport.Sent[4]);
            Assert.Equal<byte[]>([0x81, 0x01, 0x04, 0x44, 0, 0, 0x0F, 0x0F, 0xFF], _transport.Sent[5]);
            Assert.Equal(new CameraColorState(true, 34, 255), _state);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(256)]
        public void InvalidGainsCannotBeSent(int value)
        {
            Discover();
            Assert.Throws<ArgumentOutOfRangeException>(() => _camera.SetRedGain(value));
            Assert.Throws<ArgumentOutOfRangeException>(() => _camera.SetBlueGain(value));
            Assert.Equal(3, _transport.Sent.Count);
        }

        [Fact]
        public void AutomaticWhiteBalanceRejectsManualGainChanges()
        {
            Discover();
            _camera.SetManualWhiteBalance(false);
            Assert.Throws<InvalidOperationException>(() => _camera.SetRedGain(100));
            Assert.Throws<InvalidOperationException>(() => _camera.SetBlueGain(100));
        }

        [Fact]
        public void DisconnectionDropsCorrectionsAndRequiresNewDiscovery()
        {
            Discover();
            _camera.OnePushWhiteBalance();
            _camera.SetManualWhiteBalance(true);
            _camera.SetRedGain(200);
            _transport.Disconnect();
            Assert.Null(_state);
            Assert.Throws<InvalidOperationException>(() => _camera.OnePushWhiteBalance());
            _transport.Connect();
            Assert.Equal(4, _transport.Sent.Count);
            Discover();
            Assert.Equal(7, _transport.Sent.Count);
        }
    }

    private void Discover()
    {
        _camera.RefreshColor();
        _transport.Receive(0x90, 0x50, 0x05, 0xFF);
        _transport.Receive(0x90, 0x50, 0x00, 0x00, 0x08, 0x00, 0xFF);
        _transport.Receive(0x90, 0x50, 0x00, 0x00, 0x08, 0x00, 0xFF);
        Assert.NotNull(_state);
    }
}
