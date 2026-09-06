using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Threading.Channels;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Cameras.ViscaOverIp.Camera;

/// Replaces node-visca-over-ip's socket half: a connected UDP socket, and on any failure a fresh one.
///
/// UDP has no handshake, so an open socket says nothing at all about whether a camera is on the
/// other end. Pointed at an unused address it opens exactly as readily as it does at a camera, which
/// is why the TypeScript build this replaces reported every configured camera as connected for the
/// length of a service. The specification does give us a question every camera answers, so presence
/// is asked rather than assumed: the version inquiry goes out on a timer, and a camera is present
/// while it is still answering something.
public sealed class ViscaUdpTransport(string host, int port, ILogger logger) : IViscaTransport
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(1);

    /// Often enough that an operator notices a camera going away before they reach for it, and rare
    /// enough to be nothing next to the traffic a moving stick produces.
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(3);

    /// Three probes unanswered. One lost datagram is ordinary on UDP and is not a camera going away.
    private static readonly TimeSpan Silence = TimeSpan.FromSeconds(10);

    private readonly BehaviorSubject<bool> _connected = new(false);
    private readonly Subject<byte[]> _received = new();
    private readonly Channel<byte[]> _outbound = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
    {
        SingleReader = true,
    });

    private readonly CancellationTokenSource _stopping = new();
    private readonly TimeProvider _time = TimeProvider.System;
    private Task? _loop;
    private bool _reportedFailure;
    private long _lastHeard;
    private bool _heard;

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

        // An open socket is not a camera, so nothing is claimed until one answers.
        _heard = false;
        Report(connected: false);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sending = SendLoopAsync(client, linked.Token);
        var probing = ProbeLoopAsync(linked.Token);
        try
        {
            await ReceiveLoopAsync(client, linked.Token).ConfigureAwait(false);
        }
        finally
        {
            await linked.CancelAsync().ConfigureAwait(false);
            foreach (var task in new[] { sending, probing })
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected once the receive side has ended.
                }
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

    /// Any answer counts, not only the answer to the probe: a camera busy acknowledging movement is
    /// plainly there, and asking it to prove itself again while it does would be noise.
    private async Task ReceiveLoopAsync(UdpClient client, CancellationToken cancellationToken)
    {
        while (true)
        {
            var result = await client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            _lastHeard = _time.GetTimestamp();
            _heard = true;
            Report(connected: true);
            _received.OnNext(result.Buffer);
        }
    }

    private async Task ProbeLoopAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Send(ViscaPacket.Presence());
            await Task.Delay(ProbeInterval, _time, cancellationToken).ConfigureAwait(false);

            if (_heard && _time.GetElapsedTime(_lastHeard) > Silence)
            {
                logger.Log($"ViscaOverIpCamera({host}):no answer for {Silence.TotalSeconds:0} seconds");
                _heard = false;
                Report(connected: false);
            }
        }
    }
}
