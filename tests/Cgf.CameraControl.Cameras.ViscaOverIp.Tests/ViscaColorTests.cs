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
        [Theory]
        [InlineData(0x03)]
        [InlineData(0x05)]
        public void TransientInquiryRefusalRetriesWithoutAdvertisingPartialSettings(byte error)
        {
            _camera.RefreshColor();
            _transport.Refuse(socket: 0, error: error);
            Assert.Null(_state);
            _transport.Receive(0x90, 0x50, 0, 0, 8, 0, 0xFF);
            _transport.Receive(0x90, 0x50, 0, 0, 8, 0, 0xFF);
            Assert.Equal(ViscaPacket.WhiteBalanceInquiry(), _transport.Sent[^1]);
            _transport.Receive(0x90, 0x50, 5, 0xFF);
            Assert.Equal(new CameraColorState(true, 128, 128), _state);
        }

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
            Assert.Equal(ViscaPacket.Zoom(8), _transport.Sent[^1]);
            _transport.Acknowledge();
            for (var index = 0; index < 4; index++)
            {
                _time.Advance(TimeSpan.FromSeconds(5));
            }
            Assert.Null(_state);
            Assert.Equal(4, _transport.Sent.Count);
            Assert.Equal(ViscaPacket.BlueGainInquiry(), _transport.Sent[^1]);
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
        public void LostAcknowledgedOnePushCompletionReadsCurrentSettingsWithoutReplaying()
        {
            Discover();
            _camera.OnePushWhiteBalance();
            _transport.Acknowledge(socket: 1);
            _transport.Complete(socket: 1);
            _transport.Acknowledge(socket: 2);
            _time.Advance(TimeSpan.FromSeconds(5));
            Assert.Null(_state);
            Assert.Equal(ViscaPacket.WhiteBalanceInquiry(), _transport.Sent[^1]);
            _transport.Receive(0x90, 0x50, 3, 0xFF);
            _transport.Receive(0x90, 0x50, 0, 0, 9, 0, 0xFF);
            _transport.Receive(0x90, 0x50, 0, 0, 7, 0, 0xFF);
            Assert.Equal(new CameraColorState(false, 144, 112), _state);
            Assert.Equal(8, _transport.Sent.Count);
            _logger.Received().Error(Arg.Any<string>(), Arg.Is<string>(message => message.Contains("no completion")));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void RefusedGainReloadsActualSettings(bool blue, bool acknowledged)
        {
            Discover();
            if (blue)
            {
                _camera.SetBlueGain(200);
            }
            else
            {
                _camera.SetRedGain(200);
            }
            if (acknowledged)
            {
                _transport.Acknowledge();
            }
            _transport.Refuse(socket: acknowledged ? 1 : 0, error: 0x41);
            Assert.Null(_state);
            Assert.Equal(ViscaPacket.WhiteBalanceInquiry(), _transport.Sent[^1]);
            _transport.Receive(0x90, 0x50, 5, 0xFF);
            _transport.Receive(0x90, 0x50, 0, 0, 8, 0, 0xFF);
            _transport.Receive(0x90, 0x50, 0, 0, 8, 0, 0xFF);
            Assert.Equal(new CameraColorState(true, 128, 128), _state);
        }

        [Theory]
        [InlineData(ViscaCategory.PanTilt)]
        [InlineData(ViscaCategory.Zoom)]
        [InlineData(ViscaCategory.Focus)]
        public void MovementStopsPassPendingWhiteBalanceActions(ViscaCategory category)
        {
            Discover();
            _camera.Pan(1);
            _camera.SetManualWhiteBalance(false);
            _camera.SetManualWhiteBalance(true);
            switch (category)
            {
                case ViscaCategory.PanTilt:
                    _camera.Pan(0);
                    break;
                case ViscaCategory.Zoom:
                    _camera.Zoom(0);
                    break;
                case ViscaCategory.Focus:
                    _camera.Focus(0);
                    break;
            }
            _transport.Acknowledge();
            Assert.Equal(category switch
            {
                ViscaCategory.PanTilt => ViscaPacket.PanTilt(0, 0),
                ViscaCategory.Zoom => ViscaPacket.Zoom(0),
                _ => ViscaPacket.Focus(0),
            }, _transport.Sent[^1]);
            _transport.Acknowledge();
            Assert.Equal(ViscaPacket.WhiteBalance(false), _transport.Sent[^1]);
            _transport.Acknowledge();
            _transport.Complete();
            Assert.Equal(ViscaPacket.WhiteBalance(true), _transport.Sent[^1]);
        }

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
            _transport.Complete();
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
            Assert.Equal(4, _transport.Sent.Count);
            _transport.Complete(socket: 1);
            Assert.Equal(5, _transport.Sent.Count);
            _transport.Acknowledge(socket: 2);
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
            _transport.Complete();
            _transport.Acknowledge();
            _camera.OnePushWhiteBalance();
            _transport.Complete();
            _transport.Acknowledge();
            _transport.Complete();
            Assert.Equal(7, _transport.Sent.Count);
            Assert.Equal<byte[]>([0x81, 0x01, 0x04, 0x35, 0x03, 0xFF], _transport.Sent[3]);
            Assert.Equal<byte[]>([0x81, 0x01, 0x04, 0x10, 0x05, 0xFF], _transport.Sent[4]);
            Assert.Equal(_transport.Sent[3], _transport.Sent[5]);
            Assert.Equal(_transport.Sent[4], _transport.Sent[6]);
            Assert.Null(_state);
            _transport.Acknowledge();
            _transport.Complete();
            _transport.Receive(0x90, 0x50, 3, 0xFF);
            _transport.Receive(0x90, 0x50, 0, 0, 8, 0, 0xFF);
            _transport.Receive(0x90, 0x50, 0, 0, 8, 0, 0xFF);
            Assert.Equal(new CameraColorState(false, 128, 128), _state);
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
                _transport.Complete();
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
            _transport.Complete();
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

    public class ReviewRecovery : ViscaColorTests
    {
        [Fact]
        public void BufferedWhiteBalanceRetriesWaitForMovementStops()
        {
            Discover();
            _camera.SetManualWhiteBalance(false);
            _transport.Acknowledge(socket: 1);
            _camera.Focus(1);
            _camera.SetManualWhiteBalance(true);
            _camera.Pan(0);
            _transport.Refuse(socket: 1, error: 0x03);
            _transport.Acknowledge(socket: 2);
            Assert.Equal(ViscaPacket.PanTilt(0, 0), _transport.Sent[^1]);
            _transport.Acknowledge(socket: 1);
            Assert.Equal(ViscaPacket.WhiteBalance(false), _transport.Sent[^1]);
            _transport.Acknowledge(socket: 2);
            _transport.Complete(socket: 2);
            Assert.Equal(ViscaPacket.WhiteBalance(true), _transport.Sent[^1]);
        }

        [Fact]
        public void RetriedMovementStopStaysAheadOfCorrections()
        {
            Discover();
            _camera.Pan(1);
            _camera.SetRedGain(200);
            _camera.Zoom(0);
            _transport.Acknowledge();
            _transport.Refuse(socket: 0, error: 0x03);
            Assert.Equal(ViscaPacket.Zoom(0), _transport.Sent[^1]);
            _transport.Acknowledge();
            Assert.Equal(ViscaPacket.RedGain(200), _transport.Sent[^1]);
        }

        [Theory]
        [InlineData(ViscaCategory.PanTilt, 0x03)]
        [InlineData(ViscaCategory.Zoom, 0x03)]
        [InlineData(ViscaCategory.Focus, 0x03)]
        [InlineData(ViscaCategory.PanTilt, 0x05)]
        [InlineData(ViscaCategory.Zoom, 0x05)]
        [InlineData(ViscaCategory.Focus, 0x05)]
        public void WhiteBalanceRetriesWaitForMovementStops(ViscaCategory category, byte error)
        {
            Discover();
            _camera.OnePushWhiteBalance();
            switch (category)
            {
                case ViscaCategory.PanTilt:
                    _camera.Pan(0);
                    break;
                case ViscaCategory.Zoom:
                    _camera.Zoom(0);
                    break;
                case ViscaCategory.Focus:
                    _camera.Focus(0);
                    break;
            }
            _transport.Refuse(socket: 0, error: error);
            Assert.Equal(category switch
            {
                ViscaCategory.PanTilt => ViscaPacket.PanTilt(0, 0),
                ViscaCategory.Zoom => ViscaPacket.Zoom(0),
                _ => ViscaPacket.Focus(0),
            }, _transport.Sent[^1]);
            _transport.Acknowledge();
            Assert.Equal(ViscaPacket.OnePushWhiteBalanceMode(), _transport.Sent[^1]);
            _transport.Acknowledge();
            _transport.Complete();
            Assert.Equal(ViscaPacket.OnePushWhiteBalanceTrigger(), _transport.Sent[^1]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void LateExpiredSocketRepliesCannotReleaseMovement(bool refused)
        {
            Discover();
            _camera.OnePushWhiteBalance();
            _transport.Acknowledge(socket: 1);
            _transport.Complete(socket: 1);
            _transport.Acknowledge(socket: 2);
            _time.Advance(TimeSpan.FromSeconds(5));
            DiscoverReplies();
            _camera.Pan(1);
            _camera.Zoom(1);
            var sent = _transport.Sent.Count;
            if (refused)
            {
                _transport.Refuse(socket: 2, error: 0x41);
            }
            else
            {
                _transport.Complete(socket: 2);
            }
            Assert.Equal(sent, _transport.Sent.Count);
            Assert.Equal(ViscaPacket.PanTilt(24, 0), _transport.Sent[^1]);
            _time.Advance(TimeSpan.FromSeconds(5));
            Assert.Equal(ViscaPacket.Zoom(8), _transport.Sent[^1]);
            _transport.Acknowledge();
            Assert.Equal(ViscaPacket.PanTilt(24, 0), _transport.Sent[^1]);
            _logger.Received().Error(Arg.Any<string>(), Arg.Is<string>(message =>
                message.Contains("no answer to the pan and tilt command")));
        }

        [Fact]
        public void ReusedExpiredSocketTracksItsNewWhiteBalanceCommand()
        {
            Discover();
            _camera.SetManualWhiteBalance(false);
            _transport.Acknowledge(socket: 2);
            _time.Advance(TimeSpan.FromSeconds(5));
            DiscoverReplies();
            _camera.SetManualWhiteBalance(true);
            _transport.Acknowledge(socket: 2);
            _transport.Complete(socket: 2);
            Assert.Null(_state);
            Assert.Equal(ViscaPacket.WhiteBalanceInquiry(), _transport.Sent[^1]);
        }

        [Fact]
        public void BothCompletionsAfterExpiredSocketReuseLeaveUnacknowledgedMovementPending()
        {
            Discover();
            _camera.SetManualWhiteBalance(false);
            _transport.Acknowledge(socket: 1);
            _time.Advance(TimeSpan.FromSeconds(5));
            DiscoverReplies();
            _camera.Pan(1);
            _transport.Acknowledge(socket: 1);
            _camera.Focus(1);
            _camera.Zoom(1);
            _transport.Complete(socket: 1);
            _transport.Complete(socket: 1);
            _transport.Refuse(socket: 1, error: 0x03);
            Assert.Equal(9, _transport.Sent.Count);
            Assert.Equal(ViscaPacket.Focus(8), _transport.Sent[^1]);
            _transport.Acknowledge(socket: 2);
            Assert.Equal(ViscaPacket.Zoom(8), _transport.Sent[^1]);
        }

        [Fact]
        public void ATimeoutMarkerDoesNotHideRefusalOfTheReusedSocket()
        {
            Discover();
            _camera.SetManualWhiteBalance(false);
            _transport.Acknowledge(socket: 1);
            _time.Advance(TimeSpan.FromSeconds(5));
            DiscoverReplies();
            _camera.SetManualWhiteBalance(true);
            _transport.Acknowledge(socket: 1);
            _transport.Refuse(socket: 1, error: 0x03);
            Assert.Equal(9, _transport.Sent.Count);
            Assert.Equal(ViscaPacket.WhiteBalance(true), _transport.Sent[^1]);
            _transport.Acknowledge(socket: 1);
            _transport.Complete(socket: 1);
            Assert.Equal(ViscaPacket.WhiteBalanceInquiry(), _transport.Sent[^1]);
        }

        [Theory]
        [InlineData(false, 100)]
        [InlineData(false, 200)]
        [InlineData(true, 100)]
        [InlineData(true, 200)]
        public void GainsCoalesceOnlyWithinWhiteBalanceActionBarriers(bool blue, int finalValue)
        {
            Discover();
            _camera.Pan(1);
            SetGain(100);
            _camera.OnePushWhiteBalance();
            _camera.SetManualWhiteBalance(true);
            SetGain(150);
            SetGain(finalValue);
            for (var index = 0; index < 5; index++)
            {
                _transport.Acknowledge();
                _transport.Complete();
            }
            Assert.Equal(9, _transport.Sent.Count);
            Assert.Equal(GainPacket(100), _transport.Sent[4]);
            Assert.Equal(ViscaPacket.OnePushWhiteBalanceMode(), _transport.Sent[5]);
            Assert.Equal(ViscaPacket.OnePushWhiteBalanceTrigger(), _transport.Sent[6]);
            Assert.Equal(ViscaPacket.WhiteBalance(true), _transport.Sent[7]);
            Assert.Equal(GainPacket(finalValue), _transport.Sent[8]);

            void SetGain(int value)
            {
                if (blue)
                {
                    _camera.SetBlueGain(value);
                }
                else
                {
                    _camera.SetRedGain(value);
                }
            }

            byte[] GainPacket(int value) => blue ? ViscaPacket.BlueGain(value) : ViscaPacket.RedGain(value);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void GainResolutionStartsDeferredReadback(bool blue, bool refused)
        {
            Discover();
            if (blue)
            {
                _camera.SetBlueGain(200);
            }
            else
            {
                _camera.SetRedGain(200);
            }
            _transport.Acknowledge();
            _camera.RefreshColor();
            Assert.Equal(4, _transport.Sent.Count);
            if (refused)
            {
                _transport.Refuse(socket: 1, error: 0x41);
            }
            else
            {
                _transport.Complete(socket: 1);
            }
            Assert.Null(_state);
            Assert.Equal(ViscaPacket.WhiteBalanceInquiry(), _transport.Sent[^1]);
            _transport.Receive(0x90, 0x50, 5, 0xFF);
            _transport.Receive(0x90, 0x50, 0, 0, 8, 0, 0xFF);
            _transport.Receive(0x90, 0x50, 0, 0, 8, 0, 0xFF);
            Assert.Equal(new CameraColorState(true, 128, 128), _state);
            Assert.Equal(7, _transport.Sent.Count);
        }

    }

    public class CompletionOrdering : ViscaColorTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void RefreshDuringAnyInquiryStageDiscardsTheSnapshotAndStartsOneFreshSweep(int stage)
        {
            Discover();
            var published = new List<CameraColorState>();
            using var subscription = _camera.WhenColorChanged.Subscribe(state =>
            {
                if (state is not null)
                {
                    published.Add(state);
                }
            });
            published.Clear();
            _camera.RefreshColor();
            if (stage > 0)
            {
                _transport.Receive(0x90, 0x50, 5, 0xFF);
            }
            if (stage > 1)
            {
                _transport.Receive(0x90, 0x50, 0, 0, 8, 0, 0xFF);
            }
            _camera.RefreshColor();
            _camera.RefreshColor();
            if (stage == 0)
            {
                _transport.Receive(0x90, 0x50, 5, 0xFF);
            }
            if (stage < 2)
            {
                _transport.Receive(0x90, 0x50, 0, 0, 8, 0, 0xFF);
            }
            _transport.Receive(0x90, 0x50, 0, 0, 8, 0, 0xFF);
            Assert.Empty(published);
            Assert.Null(_state);
            Assert.Equal(ViscaPacket.WhiteBalanceInquiry(), _transport.Sent[^1]);
            _transport.Receive(0x90, 0x50, 0, 0xFF);
            _transport.Receive(0x90, 0x50, 0, 0, 9, 0, 0xFF);
            _transport.Receive(0x90, 0x50, 0, 0, 7, 0, 0xFF);
            Assert.Equal(new CameraColorState(false, 144, 112), Assert.Single(published));
            Assert.Equal(9, _transport.Sent.Count);
        }

        [Fact]
        public void RefusedAutomaticModeCannotRunAfterTheLaterManualSelection()
        {
            Discover();
            _camera.SetManualWhiteBalance(false);
            _camera.SetManualWhiteBalance(true);
            _transport.Acknowledge(socket: 1);
            Assert.Equal(4, _transport.Sent.Count);
            _transport.Refuse(socket: 1, error: 0x03);
            Assert.Equal(ViscaPacket.WhiteBalance(false), _transport.Sent[^1]);
            _transport.Acknowledge(socket: 1);
            Assert.Equal(5, _transport.Sent.Count);
            _transport.Complete(socket: 1);
            Assert.Equal(ViscaPacket.WhiteBalance(true), _transport.Sent[^1]);
            _transport.Acknowledge(socket: 2);
            _transport.Complete(socket: 2);
            Assert.Equal(ViscaPacket.WhiteBalanceInquiry(), _transport.Sent[^1]);
            DiscoverReplies();
            Assert.Equal(new CameraColorState(true, 128, 128), _state);
        }

        [Fact]
        public void RefusedOnePushTriggerStaysInOnePushModeUntilItCompletes()
        {
            Discover();
            _camera.OnePushWhiteBalance();
            _camera.SetManualWhiteBalance(true);
            _transport.Acknowledge(socket: 1);
            Assert.Equal(4, _transport.Sent.Count);
            _transport.Complete(socket: 1);
            Assert.Equal(ViscaPacket.OnePushWhiteBalanceTrigger(), _transport.Sent[^1]);
            _transport.Acknowledge(socket: 2);
            Assert.Equal(5, _transport.Sent.Count);
            _transport.Refuse(socket: 2, error: 0x05);
            Assert.Equal(ViscaPacket.OnePushWhiteBalanceTrigger(), _transport.Sent[^1]);
            _transport.Acknowledge(socket: 2);
            _transport.Complete(socket: 2);
            Assert.Equal(ViscaPacket.WhiteBalance(true), _transport.Sent[^1]);
            _transport.Acknowledge(socket: 1);
            _transport.Complete(socket: 1);
            DiscoverReplies();
            Assert.Equal(new CameraColorState(true, 128, 128), _state);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void GainRetriesRemainBeforeTheirActionBarrierAndLaterChannelValue(bool blue, bool acknowledged)
        {
            Discover();
            SetGain(100);
            _camera.OnePushWhiteBalance();
            _camera.SetManualWhiteBalance(true);
            SetGain(200);
            _camera.Pan(0);
            if (acknowledged)
            {
                _transport.Acknowledge(socket: 1);
            }
            _transport.Refuse(socket: acknowledged ? 1 : 0, error: 0x03);
            Assert.Equal(ViscaPacket.PanTilt(0, 0), _transport.Sent[^1]);
            _transport.Acknowledge(socket: 2);
            _transport.Complete(socket: 2);
            Assert.Equal(GainPacket(100), _transport.Sent[^1]);
            for (var index = 0; index < 4; index++)
            {
                _transport.Acknowledge(socket: 1);
                _transport.Complete(socket: 1);
            }
            Assert.Equal(10, _transport.Sent.Count);
            Assert.Equal(GainPacket(100), _transport.Sent[5]);
            Assert.Equal(ViscaPacket.OnePushWhiteBalanceMode(), _transport.Sent[6]);
            Assert.Equal(ViscaPacket.OnePushWhiteBalanceTrigger(), _transport.Sent[7]);
            Assert.Equal(ViscaPacket.WhiteBalance(true), _transport.Sent[8]);
            Assert.Equal(GainPacket(200), _transport.Sent[9]);

            void SetGain(int value)
            {
                if (blue)
                {
                    _camera.SetBlueGain(value);
                }
                else
                {
                    _camera.SetRedGain(value);
                }
            }

            byte[] GainPacket(int value) => blue ? ViscaPacket.BlueGain(value) : ViscaPacket.RedGain(value);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void NewerGainInTheSameActionStageSupersedesTheFailedGain(bool acknowledged)
        {
            Discover();
            _camera.SetRedGain(100);
            _camera.SetRedGain(200);
            if (acknowledged)
            {
                _transport.Acknowledge();
            }
            _transport.Refuse(socket: acknowledged ? 1 : 0, error: 0x03);
            Assert.Equal(5, _transport.Sent.Count);
            Assert.Equal(ViscaPacket.RedGain(200), _transport.Sent[^1]);
            _transport.Acknowledge();
            _transport.Complete();
            Assert.Equal(5, _transport.Sent.Count);
            Assert.Equal(new CameraColorState(true, 200, 128), _state);
        }

        [Theory]
        [InlineData(ViscaCategory.ColorAction)]
        [InlineData(ViscaCategory.RedGain)]
        [InlineData(ViscaCategory.BlueGain)]
        public void EveryBufferedCorrectionTimesOutWithoutReplayingDependentCommands(ViscaCategory category)
        {
            Discover();
            switch (category)
            {
                case ViscaCategory.ColorAction:
                    _camera.OnePushWhiteBalance();
                    break;
                case ViscaCategory.RedGain:
                    _camera.SetRedGain(200);
                    break;
                case ViscaCategory.BlueGain:
                    _camera.SetBlueGain(200);
                    break;
            }
            _camera.SetManualWhiteBalance(true);
            _transport.Acknowledge();
            _time.Advance(TimeSpan.FromSeconds(5));
            Assert.Equal(ViscaPacket.WhiteBalanceInquiry(), _transport.Sent[^1]);
            DiscoverReplies();
            Assert.Equal(new CameraColorState(true, 128, 128), _state);
            Assert.Equal(7, _transport.Sent.Count);
            _logger.Received().Error(Arg.Any<string>(), Arg.Is<string>(message => message.Contains("no completion")));
            _camera.Pan(0.5);
            _camera.Zoom(0.5);
            _transport.Complete(socket: 1);
            _transport.Refuse(socket: 1, error: 0x03);
            Assert.Equal(8, _transport.Sent.Count);
            _transport.Acknowledge(socket: 2);
            Assert.Equal(9, _transport.Sent.Count);
        }

        [Fact]
        public void PendingInquiryCannotHoldBackAMovementStop()
        {
            Discover();
            _camera.Pan(1);
            _transport.Acknowledge();
            _camera.RefreshColor();
            Assert.Equal(ViscaPacket.WhiteBalanceInquiry(), _transport.Sent[^1]);
            _camera.Pan(0);
            Assert.Equal(ViscaPacket.PanTilt(0, 0), _transport.Sent[^1]);
            _transport.Acknowledge();
            _transport.Receive(0x90, 0x50, 5, 0xFF);
            Assert.Equal(ViscaPacket.RedGainInquiry(), _transport.Sent[^1]);
            _transport.Receive(0x90, 0x50, 0, 0, 8, 0, 0xFF);
            _transport.Receive(0x90, 0x50, 0, 0, 8, 0, 0xFF);
            Assert.Equal(new CameraColorState(true, 128, 128), _state);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UnansweredGainIsDiscardedAndReadsCurrentSettings(bool blue)
        {
            Discover();
            if (blue)
            {
                _camera.SetBlueGain(200);
            }
            else
            {
                _camera.SetRedGain(200);
            }
            _camera.SetManualWhiteBalance(false);
            _time.Advance(TimeSpan.FromSeconds(5));
            Assert.Null(_state);
            Assert.Equal(ViscaPacket.WhiteBalanceInquiry(), _transport.Sent[^1]);
            DiscoverReplies();
            Assert.Equal(new CameraColorState(true, 128, 128), _state);
            Assert.Equal(7, _transport.Sent.Count);
            _logger.Received().Error(Arg.Any<string>(), Arg.Is<string>(message =>
                message.Contains(blue ? "no answer to the blue gain command" : "no answer to the red gain command")));
        }

    }

    private void Discover()
    {
        _camera.RefreshColor();
        DiscoverReplies();
    }

    private void DiscoverReplies()
    {
        _transport.Receive(0x90, 0x50, 5, 0xFF);
        _transport.Receive(0x90, 0x50, 0, 0, 8, 0, 0xFF);
        _transport.Receive(0x90, 0x50, 0, 0, 8, 0, 0xFF);
        Assert.NotNull(_state);
    }
}
