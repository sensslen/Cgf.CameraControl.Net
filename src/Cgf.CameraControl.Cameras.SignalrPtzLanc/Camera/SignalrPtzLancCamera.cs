using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;

public sealed class SignalrPtzLancCamera : ICameraConnection
{
    private readonly SignalrPtzLancCameraConfiguration _config;
    private readonly ISignalrStateTransport _transport;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly IDisposable _connection;

    private SignalrPtzLancCameraState _requested;
    private bool _connected;
    private bool _transmitting;
    private bool _pending;

    public SignalrPtzLancCamera(
        SignalrPtzLancCameraConfiguration config,
        ISignalrStateTransport transport,
        ILogger logger)
    {
        _config = config;
        _transport = transport;
        _logger = logger;
        _connection = transport.WhenConnectedChanged.Subscribe(OnConnectionChanged);
        transport.Start();
    }

    public string ConnectionString => _config.ConnectionUrl;

    public IObservable<bool> WhenConnectedChanged => _transport.WhenConnectedChanged;

    /// The whole state travels in one call, so the movement the controller is holding is whatever
    /// was asked for last rather than a sequence of per-axis updates that can interleave.
    public void Pan(double value) => Change(state => state with { Pan = Invert(Scale(value, 255)) });

    public void Tilt(double value) => Change(state => state with { Tilt = Invert(Scale(value, 255)) });

    public void Zoom(double value) => Change(state => state with { Zoom = Scale(value, 8) });

    // The stick reaches full speed at 0.84 of its travel, which is what the LANC focus drive takes.
    public void Focus(double value) => Change(state => state with { Focus = Scale(value * 1.2, 1) });

    /// This controller drives the camera's own LANC line and has no lamp to light.
    public void SetTally(TallyState value)
    {
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        _connection.Dispose();
        await _transport.DisposeAsync().ConfigureAwait(false);
        _stopping.Dispose();
    }

    private void Change(Func<SignalrPtzLancCameraState, SignalrPtzLancCameraState> change)
    {
        lock (_gate)
        {
            _requested = change(_requested);
            _pending = true;
            if (_transmitting || !_connected)
            {
                return;
            }

            _transmitting = true;
        }

        _ = TransmitAsync();
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
                accepted = await _transport.SetStateAsync(state, _stopping.Token).ConfigureAwait(false);
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
                continue;
            }

            // The controller rejects an update it could not apply, so the state is offered again
            // rather than left behind on a camera that is still moving.
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
            if (!connected || _transmitting || !_pending)
            {
                return;
            }

            _transmitting = true;
        }

        _ = TransmitAsync();
    }

    private void Log(string message) => _logger.Log("CgfPtzCamera", $"({_config.ConnectionUrl}) {message}");

    private int Invert(int value) => _config.PanTiltInvert ? -value : value;

    private static int Scale(double value, int maximum) =>
        (int)Math.Round(Math.Clamp(value * maximum, -maximum, maximum));
}
