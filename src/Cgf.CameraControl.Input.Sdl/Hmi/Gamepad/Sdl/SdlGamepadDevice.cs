using System.Reactive.Subjects;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using SDL3;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;

/// One configured interface's view of a pad. It outlives any particular piece of hardware: the
/// interface is built when the configuration loads, which is routinely before the pad is plugged in
/// and long before it is unplugged again.
public sealed class SdlGamepadDevice : IGamepadDevice
{
    private readonly SdlGamepadSystem _system;
    private readonly GamepadInputMapper _mapper;
    private readonly BehaviorSubject<bool> _connected = new(false);

    private volatile SdlGamepadInfo? _bound;

    private readonly bool _rumbleWanted;

    internal SdlGamepadDevice(
        SdlGamepadSystem system,
        string label,
        string? serialNumber,
        double deadzone,
        bool rumble)
    {
        _system = system;
        _mapper = new GamepadInputMapper(deadzone);
        _rumbleWanted = rumble;
        Label = label;
        SerialNumber = serialNumber;
    }

    public string Label { get; }

    public string? SerialNumber { get; }

    public SdlGamepadInfo? Bound => _bound;

    internal bool ReportedUnmatched { get; set; }

    /// An identity rather than a sentence, so the only prose the user reads stays in the localized
    /// user interface. Whether the pad is actually there is what WhenConnectedChanged is for.
    public string Description => _bound is { } info
        ? $"{Label} · {info.Name} ({info.Identity})"
        : SerialNumber is { } serial
            ? $"{Label} · {serial}"
            : Label;

    /// A pad the operator turned rumble off for reports the same thing as a pad that cannot rumble,
    /// so an interface asks one question rather than reading the configuration a second time.
    public bool SupportsRumble => _rumbleWanted && (_bound?.SupportsRumble ?? false);

    public IObservable<bool> WhenConnectedChanged => _connected;

    public IObservable<StickPosition> LeftStick => _mapper.LeftStick;

    public IObservable<StickPosition> RightStick => _mapper.RightStick;

    public IObservable<ButtonDirection> ConnectionChangeRequested => _mapper.ConnectionChangeRequested;

    public IObservable<ButtonDirection> SpecialFunctionRequested => _mapper.SpecialFunctionRequested;

    public IObservable<MixerTransition> TransitionRequested => _mapper.TransitionRequested;

    public IObservable<AltKeyConfiguration> Modifiers => _mapper.Modifiers;

    public IObservable<GamepadState> State => _mapper.State;

    public void Rumble(double intensity, TimeSpan duration)
    {
        if (_rumbleWanted)
        {
            _system.Rumble(this, intensity, duration);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _system.ReleaseAsync(this).ConfigureAwait(false);
        _mapper.Dispose();
        _connected.Dispose();
    }

    internal void Axis(SDL.GamepadAxis axis, short value) => _mapper.Axis(axis, value);

    internal void Button(SDL.GamepadButton button, bool down) => _mapper.Button(button, down);

    internal void Bind(SdlGamepadInfo info)
    {
        _bound = info;
        ReportedUnmatched = false;
        _connected.OnNext(true);
    }

    internal void Unbind()
    {
        _bound = null;
        _mapper.Reset();
        _connected.OnNext(false);
    }
}
