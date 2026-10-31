using System.Reactive.Subjects;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Hid;

public sealed class HidGamepadDevice : IGamepadDevice
{
    private readonly IHidGamepadSource _source;
    private readonly ILogger _logger;
    private readonly string _label;
    private readonly string _serial;
    private readonly GamepadInputMapper _mapper;
    private readonly F310ReportMapper _reports;
    private readonly BehaviorSubject<bool> _connected = new(false);
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _loop;
    private readonly Action<HidGamepadDevice>? _release;
    private volatile string _description;
    private bool _disposed;

    public HidGamepadDevice(IHidGamepadSource source, string label, string serial, double deadzone, ILogger logger,
        Action<HidGamepadDevice>? release = null)
    {
        _source = source;
        _label = label;
        _serial = serial;
        _logger = logger;
        _release = release;
        _description = $"{label} · {serial}";
        _mapper = new GamepadInputMapper(deadzone);
        _reports = new F310ReportMapper(_mapper);
        _loop = Task.Run(RunAsync);
    }

    public string Description => _description;
    public string SerialNumber => _serial;
    public bool SupportsRumble => false;
    public IObservable<bool> WhenConnectedChanged => _connected;
    public IObservable<StickPosition> LeftStick => _mapper.LeftStick;
    public IObservable<StickPosition> RightStick => _mapper.RightStick;
    public IObservable<ButtonDirection> ConnectionChangeRequested => _mapper.ConnectionChangeRequested;
    public IObservable<ButtonDirection> SpecialFunctionRequested => _mapper.SpecialFunctionRequested;
    public IObservable<MixerTransition> TransitionRequested => _mapper.TransitionRequested;
    public IObservable<AltKeyConfiguration> Modifiers => _mapper.Modifiers;
    public IObservable<GamepadState> State => _mapper.State;

    public void Rumble(double intensity, TimeSpan duration) { }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        await _stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await _loop.ConfigureAwait(false);
        }
        finally
        {
            _stopping.Dispose();
            _mapper.Dispose();
            _connected.Dispose();
            _release?.Invoke(this);
        }
    }

    private async Task RunAsync()
    {
        var buffer = new byte[64];
        string? lastError = null;
        try
        {
            while (!_stopping.IsCancellationRequested)
            {
                try
                {
                    using var connection = _source.Open(_serial);
                    _description = $"{_label} · {connection.Description}";
                    _logger.Log("HID", $"connected {_description}");
                    _connected.OnNext(true);
                    lastError = null;
                    while (!_stopping.IsCancellationRequested)
                    {
                        var count = connection.Read(buffer);
                        if (count == 0)
                        {
                            continue;
                        }

                        // A malformed report or a subscriber that throws costs one report, not the pad.
                        try
                        {
                            _reports.Apply(buffer.AsSpan(0, count));
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message != lastError)
                            {
                                _logger.Error("HID", $"{_label}: report failed - {ex.Message}");
                                lastError = ex.Message;
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (ex.Message != lastError)
                    {
                        _logger.Error("HID", $"{_label}: {ex.Message}");
                        lastError = ex.Message;
                    }
                }
                finally
                {
                    _reports.Reset();
                    if (_connected.Value)
                    {
                        _connected.OnNext(false);
                        _logger.Log("HID", $"disconnected {_description}");
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(1), _stopping.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
        }
    }
}
