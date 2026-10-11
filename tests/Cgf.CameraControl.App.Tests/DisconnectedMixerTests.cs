using System.Net.Sockets;
using System.Threading.Channels;
using AtemSharp.DependencyInjection;
using AtemSharp.FrameworkAbstraction;
using Avalonia.Threading;
using Cgf.CameraControl.App.ViewModels;
using Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;
using Cgf.CameraControl.Core;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Cgf.CameraControl.App.Tests;

public class DisconnectedMixerTests
{
    [Fact]
    public async Task OfflineMixersStillBuildTheKeyboardInterfaceAndDisconnectedTiles()
    {
        var packets = Channel.CreateUnbounded<UdpReceiveResult>();
        var socket = Substitute.For<IUdpClient>();
        socket.ReceiveAsync(Arg.Any<CancellationToken>())
            .Returns(call => packets.Reader.ReadAsync(call.Arg<CancellationToken>()));
        await using var provider = new ServiceCollection().AddAtemSharp()
            .AddTransient<IUdpClient>(_ => socket).BuildServiceProvider();
        var logger = Substitute.For<ILogger>();
        await using var core = new CameraControlCore(logger);
        core.MixerFactory.AddBuilder(new AtemBuilder(logger, provider.GetRequiredService<IServices>()));
        core.HmiFactory.AddBuilder(new KeyboardBuilder(core.MixerFactory, core.CameraFactory, [], logger));
        var config = ConfigLoader.Load("""
            {
              "cams": [],
              "videoMixers": [
                { "type": "blackmagicdesign/atem", "instance": 1, "ip": "192.168.1.241", "mixEffectBlock": 0 },
                { "type": "blackmagicdesign/atem", "instance": 2, "ip": "192.168.1.241", "mixEffectBlock": 1 }
              ],
              "interfaces": [
                {
                  "type": "keyboard", "instance": 3, "videoMixer": 1,
                  "connectionChange": { "type": "direct", "default": {} },
                  "cameraMap": {}, "keys": {}
                }
              ]
            }
            """, out _);

        var issues = await core.BootstrapAsync(config, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Empty(issues);
        Assert.Same(core.MixerFactory.Get(1), Assert.IsType<Gamepad>(core.HmiFactory.Get(3)).Mixer);
        using var first = new MixerViewModel(1, core.MixerFactory.Get(1)!);
        using var second = new MixerViewModel(2, core.MixerFactory.Get(2)!);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("192.168.1.241:0", first.ConnectionString);
        Assert.Equal("192.168.1.241:1", second.ConnectionString);
        Assert.False(first.IsConnected);
        Assert.False(second.IsConnected);
    }
}
