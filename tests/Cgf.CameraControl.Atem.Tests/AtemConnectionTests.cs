using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;
using AtemSharp.Commands;
using AtemSharp.Communication;
using AtemSharp.DependencyInjection;
using AtemSharp.FrameworkAbstraction;
using Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;
using Cgf.CameraControl.Core;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Logger;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Cgf.CameraControl.Atem.Tests;

public class AtemConnectionTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ForwardingUsesTheSourceCreatedDuringConnect()
    {
        var client = Substitute.For<IAtemClient>();
        var abandoned = new BufferBlock<IDeserializedCommand>();
        var current = new BufferBlock<IDeserializedCommand>();
        client.ReceivedCommands.Returns(abandoned);
        client.ConnectAsync("10.0.0.1", 9910).Returns(_ =>
        {
            client.ReceivedCommands.Returns(current);
            return Task.CompletedTask;
        });
        var seen = new List<IDeserializedCommand>();
        await using var observing = new ObservingAtemClient(client, seen.Add);
        await observing.ConnectAsync("10.0.0.1", 9910);
        var command = Substitute.For<IDeserializedCommand>();

        current.Post(command);

        Assert.Same(command, await observing.ReceivedCommands.ReceiveAsync(TestTimeout));
        Assert.Same(command, Assert.Single(seen));
        Assert.Equal(0, current.Count);
    }

    [Fact]
    public async Task OfflineMixersAreRegisteredWithoutWaitingForTheHandshake()
    {
        var socket = new TestUdpClient();
        await using var provider = Services(socket);
        var logger = Substitute.For<ILogger>();
        await using var core = new CameraControlCore(logger);
        core.MixerFactory.AddBuilder(new AtemBuilder(logger, provider.GetRequiredService<IServices>()));
        var config = ConfigLoader.Load("""
            {
              "videoMixers": [
                { "type": "blackmagicdesign/atem", "instance": 1, "ip": "10.0.0.1", "mixEffectBlock": 0 },
                { "type": "blackmagicdesign/atem", "instance": 2, "ip": "10.0.0.1", "mixEffectBlock": 1 }
              ]
            }
            """, out _);

        Assert.Empty(await core.BootstrapAsync(config, TestContext.Current.CancellationToken)
            .WaitAsync(TestTimeout, TestContext.Current.CancellationToken));

        Assert.Equal(2, core.MixerFactory.Instances.Count);
        foreach (var mixer in core.MixerFactory.Instances.Values)
        {
            Assert.False(await mixer.WhenConnectedChanged.FirstAsync().ToTask());
        }

        await socket.Started.Task.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
        Assert.Equal(1, socket.Hellos);
        await core.DisposeAsync().AsTask().WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
        Assert.True(socket.Disposed);
    }

    [Fact]
    public async Task FailedConnectionRemainsRegisteredAndReportsTheError()
    {
        var socket = new TestUdpClient { SendFailure = new SocketException((int)SocketError.NetworkUnreachable) };
        await using var provider = Services(socket);
        var failure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logger = Substitute.For<ILogger>();
        logger.When(log => log.Error("Atem", Arg.Any<string>())).Do(_ => failure.TrySetResult());
        var factory = new AtemFactory(logger, provider.GetRequiredService<IServices>());
        var connection = factory.Get("10.0.0.1");
        await connection.StartupAsync(TestContext.Current.CancellationToken);

        await failure.Task.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);

        Assert.False(connection.Connected);
        Assert.Same(connection, factory.Get("10.0.0.1"));
        await factory.ReleaseAsync("10.0.0.1");
        await factory.ReleaseAsync("10.0.0.1").WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
        Assert.True(socket.Disposed);
    }

    [Fact]
    public async Task InitializationAndReconnectUpdateBothSharedMixers()
    {
        var socket = new TestUdpClient();
        await using var provider = Services(socket);
        var factory = new AtemFactory(Substitute.For<ILogger>(), provider.GetRequiredService<IServices>());
        await using var first = new VideoMixer.Blackmagicdesign.Atem(
            new AtemConfiguration { Ip = "10.0.0.1", MixEffectBlock = 0 }, factory);
        await using var second = new VideoMixer.Blackmagicdesign.Atem(
            new AtemConfiguration { Ip = "10.0.0.1", MixEffectBlock = 1 }, factory);
        var connected = first.WhenConnectedChanged.Where(value => value).FirstAsync().ToTask();
        await first.StartupAsync(TestContext.Current.CancellationToken);
        await second.StartupAsync(TestContext.Current.CancellationToken);
        await socket.Started.Task.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);

        socket.Initialize();
        await connected.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
        Assert.True(await second.WhenConnectedChanged.FirstAsync().ToTask());

        var disconnected = first.WhenConnectedChanged.Where(value => !value).FirstAsync().ToTask();
        // Stop receiving: the real protocol's watchdog must clear both connection lamps.
        await disconnected.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
        Assert.False(await second.WhenConnectedChanged.FirstAsync().ToTask());

        var reconnected = first.WhenConnectedChanged.Where(value => value).FirstAsync().ToTask();
        socket.Initialize();
        await reconnected.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ReleasingOneBlockDoesNotStopTheOtherBlocksPendingConnection()
    {
        var socket = new TestUdpClient();
        await using var provider = Services(socket);
        var factory = new AtemFactory(Substitute.For<ILogger>(), provider.GetRequiredService<IServices>());
        var first = factory.Get("10.0.0.1");
        var second = factory.Get("10.0.0.1");
        await first.StartupAsync(TestContext.Current.CancellationToken);
        await socket.Started.Task.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);

        await factory.ReleaseAsync("10.0.0.1");

        Assert.Same(first, second);
        Assert.False(socket.Disposed);
        await factory.ReleaseAsync("10.0.0.1").WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
        Assert.True(socket.Disposed);
    }

    private static ServiceProvider Services(TestUdpClient socket) =>
        new ServiceCollection().AddAtemSharp().AddTransient<IUdpClient>(_ => socket).BuildServiceProvider();

    private sealed class TestUdpClient : IUdpClient
    {
        private readonly Channel<UdpReceiveResult> _received = Channel.CreateUnbounded<UdpReceiveResult>();
        private readonly IPEndPoint _endpoint = new(IPAddress.Parse("10.0.0.1"), 9910);

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Hellos { get; private set; }

        public bool Disposed { get; private set; }

        public Exception? SendFailure { get; init; }

        public void Bind(IPEndPoint remoteEndpoint) { }

        public void Connect(IPEndPoint remoteEndPoint) { }

        public ValueTask<int> SendAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            if (SendFailure is { } failure)
            {
                throw failure;
            }

            if (AtemPacket.FromBytes(data).HasFlag(PacketFlag.NewSessionId))
            {
                Hellos++;
                Started.TrySetResult();
            }

            return ValueTask.FromResult(data.Length);
        }

        public ValueTask<UdpReceiveResult> ReceiveAsync(CancellationToken cancellationToken = default) =>
            _received.Reader.ReadAsync(cancellationToken);

        public void Initialize()
        {
            _received.Writer.TryWrite(new UdpReceiveResult(new AtemPacket([])
            {
                Flags = PacketFlag.NewSessionId, SessionId = 1, PacketId = 1,
            }.ToBytes(), _endpoint));
            _received.Writer.TryWrite(new UdpReceiveResult(new AtemPacket([0, 8, 0, 0, 73, 110, 67, 109])
            {
                Flags = PacketFlag.AckRequest, SessionId = 1, PacketId = 2,
            }.ToBytes(), _endpoint));
        }

        public void Dispose() => Disposed = true;
    }
}
