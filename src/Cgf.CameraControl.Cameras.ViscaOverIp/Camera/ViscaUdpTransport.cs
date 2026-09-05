using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Threading.Channels;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Cameras.ViscaOverIp.Camera;

/// Replaces node-visca-over-ip's socket half: a connected UDP socket, and on any failure a fresh one.
///
/// UDP has no handshake, so there is nothing to wait for and nothing that reports the camera being
/// switched off. The socket being open is reported as connected, exactly as the TypeScript camera
/// treats it. What does arrive is an ICMP port unreachable, which the connected socket surfaces as a
/// receive error, and that is what takes the connection back down.
public sealed class ViscaUdpTransport(string host, int port, ILogger logger) : IViscaTransport
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(1);

    private readonly BehaviorSubject<bool> _connected = new(false);
    private readonly Subject<byte[]> _received = new();
    private readonly Channel<byte[]> _outbound = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
    {
        SingleReader = true,
    });

    private readonly CancellationTokenSource _stopping = new();
    private Task? _loop;
    private bool _reportedFailure;

    public IObservable<bool> WhenConnectedChanged => _connected;

    public IObservable<byte[]> Received => _received;

    public void Start() => _loop ??= Task.Run(() => RunAsync(_stopping.Token));

    public void Send(byte[] packet) => _outbound.Writer.TryWrite(packet);

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected while shutting down.
            }
        }

        _stopping.Dispose();
        _connected.Dispose();
        _received.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndPumpAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // A camera that is switched off is retried once a second for the length of a
                // service. Reporting each attempt would bury everything else in the log, so the
                // first failure is reported and the rest are silent until it comes back.
                if (!_reportedFailure)
                {
                    _reportedFailure = true;
                    logger.Log($"ViscaOverIpCamera({host}):connection lost - {ex.Message}");
                }
            }

            Report(connected: false);
            await Task.Delay(ReconnectDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    private void Report(bool connected)
    {
        if (_connected.Value != connected)
        {
            _connected.OnNext(connected);
        }
    }

    private async Task ConnectAndPumpAsync(CancellationToken cancellationToken)
    {
        using var client = new UdpClient();
        client.Connect(host, port);
        _reportedFailure = false;
        logger.Log($"ViscaOverIpCamera({host}):connected");

        // Draining anything queued while disconnected would replay stale movement, so the sender
        // starts from whatever the camera asks for next.
        while (_outbound.Reader.TryRead(out _))
        {
        }

        Report(connected: true);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sending = SendLoopAsync(client, linked.Token);
        try
        {
            await ReceiveLoopAsync(client, linked.Token).ConfigureAwait(false);
        }
        finally
        {
            await linked.CancelAsync().ConfigureAwait(false);
            try
            {
                await sending.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected once the receive side has ended.
            }
        }
    }

    private async Task SendLoopAsync(UdpClient client, CancellationToken cancellationToken)
    {
        await foreach (var packet in _outbound.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            await client.SendAsync(packet, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReceiveLoopAsync(UdpClient client, CancellationToken cancellationToken)
    {
        while (true)
        {
            var result = await client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            _received.OnNext(result.Buffer);
        }
    }
}
