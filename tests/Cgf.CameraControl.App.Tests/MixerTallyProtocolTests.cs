using System.Reactive.Linq;
using System.Reactive.Subjects;
using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Core.VideoMixer.Passthrough;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;
using NSubstitute;

namespace Cgf.CameraControl.App.Tests;

public class MixerTallyProtocolTests
{
    public class ViscaSignals
    {
        [Theory]
        [InlineData("preview-program", ViscaTallyMode.Avonic)]
        [InlineData("program", ViscaTallyMode.Avonic)]
        [InlineData("none", ViscaTallyMode.Avonic)]
        [InlineData("preview-program", ViscaTallyMode.PtzOptics)]
        [InlineData("program", ViscaTallyMode.PtzOptics)]
        [InlineData("none", ViscaTallyMode.PtzOptics)]
        public async Task MixerPolicyReachesTheCameraInItsSelectedProtocol(string mode, ViscaTallyMode protocol)
        {
            var logger = Substitute.For<ILogger>();
            using var replies = new Subject<byte[]>();
            var transport = Substitute.For<IViscaTransport>();
            transport.WhenConnectedChanged.Returns(Observable.Return(true));
            transport.Received.Returns(replies);
            var tallies = new List<byte[]>();
            transport.When(t => t.Send(Arg.Any<byte[]>())).Do(call =>
            {
                var packet = call.Arg<byte[]>();
                if (Enum.GetValues<TallyState>().Any(state =>
                    packet.SequenceEqual(ViscaTally.Payload(protocol, state) ?? [])))
                {
                    tallies.Add(packet);
                }

                replies.OnNext([0x90, 0x51, 0xFF]);
            });
            await using var camera = new ViscaOverIpCamera(
                new ViscaOverIpCameraConfiguration { Ip = "127.0.0.1", TallyMode = protocol }, transport, logger);
            var entry = Assert.Single(ConfigLoader.Load($$"""
                {"videoMixers":[{"type":"passthrough/default","instance":1,"tallyMode":"{{mode}}"}]}
                """, out _).VideoMixers);
            await using var mixer = await new PassthroughBuilder(logger).BuildAsync(entry, TestContext.Current.CancellationToken);
            await using var device = new ControlSurfaceDevice(1);
            await using var keyboard = new Gamepad(
                new KeyboardConfiguration
                {
                    VideoMixer = 1,
                    CameraMap = new Dictionary<int, int> { [1] = 1 },
                    ConnectionChange = new DirectConnectionChangeConfiguration { Default = new Dictionary<ButtonDirection, int>() },
                }, device, mixer, _ => camera, logger);

            mixer.ChangeInput(1);
            Assert.Equal(ViscaTally.Payload(protocol, mode == "preview-program" ? TallyState.Preview : TallyState.Off),
                tallies[^1]);

            mixer.Cut();
            Assert.Equal(ViscaTally.Payload(protocol, mode == "none" ? TallyState.Off : TallyState.Program),
                tallies[^1]);

            mixer.ChangeInput(2);
            mixer.Cut();
            Assert.Equal(ViscaTally.Payload(protocol, mode == "preview-program" ? TallyState.Preview : TallyState.Off),
                tallies[^1]);
        }
    }
}
