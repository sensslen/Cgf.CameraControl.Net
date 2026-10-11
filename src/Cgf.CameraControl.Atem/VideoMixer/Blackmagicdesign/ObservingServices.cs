using System.Net;
using System.Threading.Tasks.Dataflow;
using AtemSharp.Commands;
using AtemSharp.Communication;
using AtemSharp.DependencyInjection;
using AtemSharp.FrameworkAbstraction;
using AtemSharp.Lib;
using Microsoft.Extensions.DependencyInjection;

namespace Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;

/// Taps the received command stream on its way to the switcher. The client replaces its source
/// during ConnectAsync, so linking before that call silently loses every initialization command.
internal sealed class ObservingAtemClient(IAtemClient inner, Action<IDeserializedCommand> tap) : IAtemClient
{
    private readonly BufferBlock<IDeserializedCommand> _forwarded = new();
    private ActionBlock<IDeserializedCommand>? _pump;
    private IDisposable? _link;

    public IReceivableSourceBlock<IDeserializedCommand> ReceivedCommands => _forwarded;

    public async Task ConnectAsync(string address, int port)
    {
        await inner.ConnectAsync(address, port).ConfigureAwait(false);
        _link?.Dispose();
        _pump = new ActionBlock<IDeserializedCommand>(command =>
        {
            tap(command);
            _forwarded.Post(command);
        });
        _link = inner.ReceivedCommands.LinkTo(_pump);
    }

    public Task DisconnectAsync() => inner.DisconnectAsync();

    public Task SendCommandAsync(SerializedCommand command) => inner.SendCommandAsync(command);

    public Task SendCommandsAsync(IEnumerable<SerializedCommand> commands) => inner.SendCommandsAsync(commands);

    public async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync().ConfigureAwait(false);
        _link?.Dispose();
        if (_pump is not null)
        {
            _pump.Complete();
            await _pump.Completion.ConfigureAwait(false);
        }

        _forwarded.Complete();
    }
}

/// AtemSharp does not cancel a pending protocol handshake or expose its automatic reconnects.
/// Build its client and protocol against this service graph so both can be supervised.
internal sealed class ObservingServices : IServices, IAsyncDisposable
{
    private readonly IServices _inner;
    private readonly Action<IDeserializedCommand> _tap;
    private readonly Action _reconnecting;
    private readonly CancellationToken _stopping;
    private readonly ServiceProvider _provider;

    public ObservingServices(
        IServices inner,
        Action<IDeserializedCommand> tap,
        Action reconnecting,
        CancellationToken stopping)
    {
        _inner = inner;
        _tap = tap;
        _reconnecting = reconnecting;
        _stopping = stopping;
        _provider = new ServiceCollection().AddAtemSharp().AddSingleton<IServices>(this).BuildServiceProvider();
    }

    public ITimeProvider TimeProvider => _inner.TimeProvider;

    public IAtemClient CreateAtemClient() =>
        new ObservingAtemClient(_provider.GetRequiredService<IAtemClient>(), _tap);

    public IAtemProtocol CreateAtemProtocol() =>
        new StoppingAtemProtocol(_provider.GetRequiredService<IAtemProtocol>(), _stopping);

    public ICommandParser CreateCommandParser() => _inner.CreateCommandParser();

    public IPacketBuilder CreatePacketBuilder() => _inner.CreatePacketBuilder();

    public IUdpClient CreateUdpClient() => new ObservingUdpClient(_inner.CreateUdpClient(), _reconnecting);

    public IActionLoop StartActionLoop(Func<CancellationToken, Task> loopedAction, string name) =>
        _inner.StartActionLoop(loopedAction, name);

    // The provider owns the protocol even when the client's pending handshake cannot disconnect it.
    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}

internal sealed class StoppingAtemProtocol(IAtemProtocol inner, CancellationToken stopping) : IAtemProtocol
{
    private bool _disposed;

    public IReceivableSourceBlock<AtemPacket> ReceivedPackets => inner.ReceivedPackets;

    public IReceivableSourceBlock<int> AckedTrackingIds => inner.AckedTrackingIds;

    public Task ConnectAsync(IPEndPoint endPoint) => inner.ConnectAsync(endPoint).WaitAsync(stopping);

    public Task DisconnectAsync() => inner.DisconnectAsync();

    public Task SendPacketsAsync(AtemPacket[] packets) => inner.SendPacketsAsync(packets);

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await inner.DisposeAsync().ConfigureAwait(false);
        }
    }
}

internal sealed class ObservingUdpClient(IUdpClient inner, Action reconnecting) : IUdpClient
{
    public void Bind(IPEndPoint remoteEndpoint) => inner.Bind(remoteEndpoint);

    public void Connect(IPEndPoint remoteEndPoint) => inner.Connect(remoteEndPoint);

    public ValueTask<int> SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (AtemPacket.FromBytes(data).HasFlag(PacketFlag.NewSessionId))
        {
            reconnecting();
        }

        return inner.SendAsync(data, cancellationToken);
    }

    public async ValueTask<System.Net.Sockets.UdpReceiveResult> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        var received = await inner.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        if (AtemPacket.FromBytes(received.Buffer).HasFlag(PacketFlag.NewSessionId))
        {
            reconnecting();
        }

        return received;
    }

    public void Dispose() => inner.Dispose();
}
