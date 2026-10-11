using Cgf.CameraControl.Core;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Core.VideoMixer.Passthrough;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Hid;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;
using NSubstitute;

namespace Cgf.CameraControl.App.Tests;

public class HidInterfaceTests
{
    [Fact]
    public async Task ConfiguredBackendOpensEachInterfaceBySerialAndSurvivesReload()
    {
        var logger = Substitute.For<ILogger>();
        var source = Substitute.For<IHidGamepadSource>();
        source.Open(Arg.Any<string>()).Returns(call =>
        {
            var connection = Substitute.For<IHidGamepadConnection>();
            connection.Description.Returns($"F310 {call.Arg<string>()}");
            connection.Read(Arg.Any<byte[]>()).Returns(_ =>
            {
                Thread.Sleep(10);
                return 0;
            });
            return connection;
        });
        await using var sdl = new SdlGamepadSystem(logger);
        await using var hid = new HidGamepadSystem(logger, source);
        await using var core = new CameraControlCore(logger);
        core.MixerFactory.AddBuilder(new PassthroughBuilder(logger));
        core.HmiFactory.AddBuilder(new GamepadBuilder(sdl, core.MixerFactory, core.CameraFactory, logger, hid));
        var config = ConfigLoader.Load("""
            {
              "videoMixers": [{ "type": "passthrough/default", "instance": 1 }],
              "interfaces": [
                {
                  "type": "gamepad", "instance": 1, "backend": "hid", "serialNumber": "83234F94",
                  "videoMixer": 1, "connectionChange": { "type": "direct", "default": {} }, "cameraMap": {}
                },
                {
                  "type": "gamepad", "instance": 2, "backend": "hid", "serialNumber": "CAD649D7",
                  "videoMixer": 1, "connectionChange": { "type": "direct", "default": {} }, "cameraMap": {}
                }
              ]
            }
            """, out _);
        for (var load = 0; load < 2; load++)
        {
            Assert.Empty(await core.ReconfigureAsync(config, TestContext.Current.CancellationToken));
            Assert.Equal(2, core.HmiFactory.Instances.Count);
            foreach (var instance in core.HmiFactory.Instances.Values)
            {
                var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var subscription = instance.WhenConnectedChanged.Subscribe(value =>
                {
                    if (value)
                    {
                        connected.TrySetResult();
                    }
                });
                await connected.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
        }
        source.Received(2).Open("83234F94");
        source.Received(2).Open("CAD649D7");
    }
}
