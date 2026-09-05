using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

/// A control surface driven by a keyboard and a mouse, reported as the pad it stands in for.
///
/// Everything above this seam already exists: which camera is on preview, stopping the one being
/// left, tally, the connection change scheme and the special functions are all in Gamepad. Writing a
/// second copy of that for a mouse would be a second place for it to be wrong, so this reports the
/// same events a pad does and nothing downstream can tell the difference.
///
/// Given a pad, it merges rather than replaces: the interface is then driven from the desk and from
/// the window at the same time, which is one interface with two sets of hands rather than two
/// interfaces fighting over the same cameras.
public sealed class ControlSurfaceDevice(int instance, IGamepadDevice? pad = null) : IGamepadDevice
{
    private readonly Subject<StickPosition> _left = new();
    private readonly Subject<StickPosition> _right = new();
    private readonly Subject<ButtonDirection> _connectionChange = new();
    private readonly Subject<ButtonDirection> _specialFunction = new();
    private readonly Subject<MixerTransition> _transition = new();
    private readonly BehaviorSubject<AltKeyConfiguration> _modifiers = new(AltKeyConfiguration.None);

    // A window is not something that gets unplugged.
    private readonly BehaviorSubject<bool> _connected = new(true);

    public int Instance { get; } = instance;

    public string Description => pad?.Description ?? $"keyboard and mouse[{Instance}]";

    public bool SupportsRumble => pad?.SupportsRumble ?? false;

    public IObservable<bool> WhenConnectedChanged => pad?.WhenConnectedChanged ?? _connected;

    public IObservable<StickPosition> LeftStick => Merge(_left, pad?.LeftStick);

    public IObservable<StickPosition> RightStick => Merge(_right, pad?.RightStick);

    public IObservable<ButtonDirection> ConnectionChangeRequested =>
        Merge(_connectionChange, pad?.ConnectionChangeRequested);

    public IObservable<ButtonDirection> SpecialFunctionRequested =>
        Merge(_specialFunction, pad?.SpecialFunctionRequested);

    public IObservable<MixerTransition> TransitionRequested => Merge(_transition, pad?.TransitionRequested);

    /// The modifiers held on the window and the modifiers held on the pad are one state, so whichever
    /// moved last is the one that counts.
    public IObservable<AltKeyConfiguration> Modifiers => Merge(_modifiers, pad?.Modifiers);

    /// Pan on X, tilt on Y, both in [-1 .. 1].
    public void Move(double pan, double tilt) => _left.OnNext(new StickPosition(pan, tilt));

    /// Focus on X, zoom on Y, both in [-1 .. 1].
    public void Lens(double focus, double zoom) => _right.OnNext(new StickPosition(focus, zoom));

    public void Select(ButtonDirection direction) => _connectionChange.OnNext(direction);

    public void Run(ButtonDirection direction) => _specialFunction.OnNext(direction);

    public void Transition(MixerTransition kind) => _transition.OnNext(kind);

    public void SetModifiers(bool alt, bool altLower)
    {
        var wanted = new AltKeyConfiguration(alt, altLower);
        if (!_modifiers.Value.Equals(wanted))
        {
            _modifiers.OnNext(wanted);
        }
    }

    public void Rumble(double intensity, TimeSpan duration) => pad?.Rumble(intensity, duration);

    public async ValueTask DisposeAsync()
    {
        if (pad is not null)
        {
            await pad.DisposeAsync().ConfigureAwait(false);
        }

        _left.Dispose();
        _right.Dispose();
        _connectionChange.Dispose();
        _specialFunction.Dispose();
        _transition.Dispose();
        _modifiers.Dispose();
        _connected.Dispose();
    }

    private static IObservable<T> Merge<T>(IObservable<T> own, IObservable<T>? other) =>
        other is null ? own : own.Merge(other);
}
