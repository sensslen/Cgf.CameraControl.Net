using System.Net.WebSockets;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Channels;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;

/// Replaces the websocket-reconnect package: connect, pump, and on any failure wait and start again.
public sealed class WebsocketTransport(Uri address, ILogger logger) : IWebsocketTransport
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(1);

    private readonly BehaviorSubject<bool> _connected = new(false);
    private readonly Subject<string> _received = new();
    private readonly Channel<string> _outbound = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
    });

    private readonly CancellationTokenSource _stopping = new();
    private Task? _loop;
    private bool _reportedFailure;

    public IObservable<bool> WhenConnectedChanged => _connected;

    public IObservable<string> Received => _received;

    public void Start() => _loop ??= Task.Run(() => RunAsync(_stopping.Token));

    public void Send(string payload) => _outbound.Writer.TryWrite(payload);

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
                    logger.Log("WebsocketCamera", $"({address}) connection lost - {ex.Message}");
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
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(address, cancellationToken).ConfigureAwait(false);
        _reportedFailure = false;
        logger.Log("WebsocketCamera", $"({address}) connected");
        Report(connected: true);

        // Draining anything queued while disconnected would replay stale movement, so the sender
        // starts from whatever the camera asks for next.
        while (_outbound.Reader.TryRead(out _))
        {
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sending = SendLoopAsync(socket, linked.Token);
        try
        {
            await ReceiveLoopAsync(socket, linked.Token).ConfigureAwait(false);
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

    private async Task SendLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        await foreach (var payload in _outbound.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            await socket
                .SendAsync(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, true, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        var message = new StringBuilder();

        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return;
            }

            message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if (!result.EndOfMessage)
            {
                continue;
            }

            _received.OnNext(message.ToString());
            message.Clear();
        }
    }
}
