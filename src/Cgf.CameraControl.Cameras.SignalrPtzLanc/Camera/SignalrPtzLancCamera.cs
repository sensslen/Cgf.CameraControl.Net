using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;

public sealed class SignalrPtzLancCamera : ICameraConnection
{
    /// A controller that keeps refusing would otherwise be asked again as fast as it can answer,
    /// and every failure logged on the way.
    public static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(1);

    private readonly SignalrPtzLancCameraConfiguration _config;
    private readonly ISignalrStateTransport _transport;
    private readonly ILogger _logger;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _stopping;
    private readonly IDisposable _connection;

    private SignalrPtzLancCameraState _requested;
    private bool _connected;
    private bool _transmitting;
    private bool _pending;
    private bool _disposed;
    private bool _shutdownStopAccepted;
    private Task _transmission = Task.CompletedTask;
    private Task? _shutdown;

    public SignalrPtzLancCamera(
        SignalrPtzLancCameraConfiguration config,
        ISignalrStateTransport transport,
        ILogger logger,
        TimeProvider? time = null)
    {
        _config = config;
        _transport = transport;
        _logger = logger;
        _time = time ?? TimeProvider.System;
        _stopping = new CancellationTokenSource(Timeout.InfiniteTimeSpan, _time);
        _connection = transport.WhenConnectedChanged.Subscribe(OnConnectionChanged);
        transport.Start();
    }

    public string ConnectionString => _config.ConnectionUrl;

    public IObservable<bool> WhenConnectedChanged => _transport.WhenConnectedChanged;

    /// The whole state travels in one call, so the movement the controller is holding is whatever
    /// was asked for last rather than a sequence of per-axis updates that can interleave.
    public void Pan(double value) => Change(state => state with { Pan = Invert(ScalePanTilt(value)) });

    public void Tilt(double value) => Change(state => state with { Tilt = Invert(ScalePanTilt(value)) });

    public void Zoom(double value) => Change(state => state with { Zoom = Scale(value, 8) });

    // The stick reaches full speed at 0.84 of its travel, which is what the LANC focus drive takes.
    public void Focus(double value) => Change(state => state with { Focus = Scale(value * 1.2, 1) });

    /// This controller drives the camera's own LANC line and has no lamp to light.
    public void SetTally(TallyState value)
    {
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            return new ValueTask(_shutdown ??= ShutdownAsync());
        }
    }

    private async Task ShutdownAsync()
    {
        Task transmission;
        lock (_gate)
        {
            _disposed = true;
            _requested = default;
            _pending = true;
            _stopping.CancelAfter(ShutdownTimeout);
            if (_connected && !_transmitting)
            {
                _transmitting = true;
                _transmission = TransmitAsync();
            }
            transmission = _transmission;
        }

        try
        {
            await transmission.ConfigureAwait(false);
            if (!_shutdownStopAccepted)
            {
                _logger.Error("CgfPtzCamera", $"({_config.ConnectionUrl}) shutdown stop was not acknowledged");
            }
        }
        finally
        {
            await _stopping.CancelAsync().ConfigureAwait(false);
            _connection.Dispose();
            await _transport.DisposeAsync().ConfigureAwait(false);
            _stopping.Dispose();
        }
    }

    private void Change(Func<SignalrPtzLancCameraState, SignalrPtzLancCameraState> change)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _requested = change(_requested);
            _pending = true;
            if (_transmitting || !_connected)
            {
                return;
            }

            _transmitting = true;
            _transmission = TransmitAsync();
        }
    }

    /// One call is in flight at a time and the latest state wins, so a stick that is moved faster
    /// than the controller answers does not build a backlog of stale positions.
    private async Task TransmitAsync()
    {
        while (true)
        {
            SignalrPtzLancCameraState state;
            lock (_gate)
            {
                if (!_pending || !_connected || _stopping.IsCancellationRequested)
                {
                    _transmitting = false;
                    return;
                }

                _pending = false;
                state = _requested;
            }

            bool accepted;
            try
            {
                accepted = await _transport.SetStateAsync(state, _stopping.Token)
                    .WaitAsync(_stopping.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                lock (_gate)
                {
                    _transmitting = false;
                }

                return;
            }
            catch (Exception ex)
            {
                Log($"state transmission error - {ex.Message}");
                accepted = false;
            }

            if (accepted)
            {
                lock (_gate)
                {
                    if (_disposed && state == default)
                    {
                        _shutdownStopAccepted = true;
                    }
                }
                continue;
            }

            // The controller rejects an update it could not apply, so the state is offered again
            // rather than left behind on a camera that is still moving.
            try
            {
                await Task.Delay(RetryDelay, _time, _stopping.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                lock (_gate)
                {
                    _transmitting = false;
                }

                return;
            }

            lock (_gate)
            {
                _pending = true;
            }
        }
    }

    private void OnConnectionChanged(bool connected)
    {
        lock (_gate)
        {
            _connected = connected;
            if (_disposed || !connected || _transmitting || !_pending)
            {
                return;
            }

            _transmitting = true;
            _transmission = TransmitAsync();
        }
    }

    private void Log(string message) => _logger.Log("CgfPtzCamera", $"({_config.ConnectionUrl}) {message}");

    private int Invert(int value) => _config.PanTiltInvert ? -value : value;

    private int ScalePanTilt(double value) =>
        Scale(Math.Clamp(value, -1, 1) * (_config.PanTiltSpeedPercent / 100.0), 255);

    private static int Scale(double value, int maximum) =>
        (int)Math.Round(Math.Clamp(value * maximum, -maximum, maximum));
}
