using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;
using Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;
using NSubstitute;
using SDL3;

namespace Cgf.CameraControl.App.Tests;

public class GamepadDirectionTests
{
    public class PanTilt
    {
        [Theory]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(false, false)]
        [InlineData(false, true)]
        public async Task BothProtocolsFollowPhysicalStickTravelAndPerCameraInversion(bool visca, bool inverted)
        {
            using var mapper = new GamepadInputMapper(0);
            using var viscaReplies = new Subject<byte[]>();
            using var websocketReplies = new Subject<string>();
            List<byte[]> packets = [];
            List<WebsocketPtzLancCameraSpeedState> states = [];
            await using var camera = visca
                ? CreateVisca(inverted, viscaReplies, packets)
                : CreateWebsocket(inverted, websocketReplies, states);
            await using var gamepad = CreateGamepad(mapper, camera);

            (short X, short Y, int Pan, int Tilt)[] movements =
            [
                (short.MaxValue, 0, 1, 0),
                (short.MinValue, 0, -1, 0),
                (0, short.MinValue, 0, 1),
                (0, short.MaxValue, 0, -1),
                (short.MaxValue, short.MinValue, 1, 1),
                (short.MinValue, short.MaxValue, -1, -1),
                (0, 0, 0, 0),
            ];

            foreach (var movement in movements)
            {
                mapper.Axis(SDL.GamepadAxis.LeftX, movement.X);
                mapper.Axis(SDL.GamepadAxis.LeftY, movement.Y);
                var pan = inverted ? -movement.Pan : movement.Pan;
                var tilt = inverted ? -movement.Tilt : movement.Tilt;

                if (visca)
                {
                    Assert.NotEmpty(packets);
                    var packet = packets[^1];
                    Assert.Equal(pan switch { -1 => 0x01, 1 => 0x02, _ => 0x03 }, packet[6]);
                    Assert.Equal(tilt switch { -1 => 0x02, 1 => 0x01, _ => 0x03 }, packet[7]);
                }
                else
                {
                    Assert.NotEmpty(states);
                    Assert.Equal(pan * 255, states[^1].Pan);
                    Assert.Equal(tilt * 255, states[^1].Tilt);
                }
            }
        }
    }

    private static ICameraConnection CreateVisca(bool inverted, Subject<byte[]> replies, List<byte[]> packets)
    {
        var transport = Substitute.For<IViscaTransport>();
        transport.WhenConnectedChanged.Returns(Observable.Return(true));
        transport.Received.Returns(replies);
        transport.When(t => t.Send(Arg.Any<byte[]>())).Do(call =>
        {
            packets.Add(call.Arg<byte[]>());
            replies.OnNext([0x90, 0x41, 0xFF]);
            replies.OnNext([0x90, 0x51, 0xFF]);
        });
        return new ViscaOverIpCamera(
            new ViscaOverIpCameraConfiguration { Ip = "127.0.0.1", PanTiltInvert = inverted },
            transport,
            Substitute.For<ILogger>());
    }

    private static ICameraConnection CreateWebsocket(
        bool inverted, Subject<string> replies, List<WebsocketPtzLancCameraSpeedState> states)
    {
        var transport = Substitute.For<IWebsocketTransport>();
        transport.WhenConnectedChanged.Returns(Observable.Return(true));
        transport.Received.Returns(replies);
        transport.When(t => t.Send(Arg.Any<string>())).Do(call =>
        {
            var payload = call.Arg<string>();
            states.Add(JsonSerializer.Deserialize(payload,
                WebsocketPtzLancCameraStateContext.Default.WebsocketPtzLancCameraSpeedState));
            replies.OnNext(payload);
        });
        var camera = new WebsocketPtzLancCamera(
            new WebsocketPtzLancCameraConfiguration
            {
                Ip = "127.0.0.1",
                PanTiltInvert = inverted,
                ShowTallyLight = false,
            },
            transport);
        replies.OnNext("{}");
        return camera;
    }

    private static Gamepad CreateGamepad(GamepadInputMapper mapper, ICameraConnection camera)
    {
        var device = Substitute.For<IGamepadDevice>();
        device.LeftStick.Returns(mapper.LeftStick);
        device.RightStick.Returns(Observable.Never<StickPosition>());
        device.Modifiers.Returns(Observable.Return(AltKeyConfiguration.None));
        device.ConnectionChangeRequested.Returns(Observable.Never<ButtonDirection>());
        device.SpecialFunctionRequested.Returns(Observable.Never<ButtonDirection>());
        device.TransitionRequested.Returns(Observable.Never<MixerTransition>());
        var mixer = Substitute.For<IVideoMixer>();
        mixer.TallyMode.Returns(MixerTallyMode.PreviewProgram);
        mixer.WhenPreviewChanged.Returns(Observable.Return(new PreviewChange(1, false)));
        mixer.WhenProgramChanged.Returns(Observable.Never<int>());
        mixer.WhenConnectedChanged.Returns(Observable.Return(true));
        return new Gamepad(
            new GamepadConfiguration
            {
                VideoMixer = 1,
                CameraMap = new Dictionary<int, int> { [1] = 1 },
                ConnectionChange = new DirectConnectionChangeConfiguration
                {
                    Default = new Dictionary<ButtonDirection, int>(),
                },
            },
            device, mixer, _ => camera, Substitute.For<ILogger>());
    }
}
