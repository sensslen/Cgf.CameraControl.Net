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
/// It stands alone rather than wrapping a pad. Two interfaces pointed at one mixer is how a desk
/// gets both, and it says so in the configuration file instead of appearing by itself.
public sealed class ControlSurfaceDevice(int instance) : IGamepadDevice
{
    private readonly Subject<StickPosition> _left = new();
    private readonly Subject<StickPosition> _right = new();
    private readonly Subject<ButtonDirection> _connectionChange = new();
    private readonly Subject<int> _input = new();
    private readonly Subject<string> _function = new();
    private readonly Subject<MixerTransition> _transition = new();
    private readonly BehaviorSubject<AltKeyConfiguration> _modifiers = new(AltKeyConfiguration.None);
    private readonly BehaviorSubject<GamepadState> _state = new(default);

    // A window is not something that gets unplugged.
    private readonly BehaviorSubject<bool> _connected = new(true);

    public int Instance { get; } = instance;

    public string Description => $"keyboard and mouse[{Instance}]";

    public bool SupportsRumble => false;

    public IObservable<bool> WhenConnectedChanged => _connected;

    public IObservable<StickPosition> LeftStick => _left;

    public IObservable<StickPosition> RightStick => _right;

    public IObservable<ButtonDirection> ConnectionChangeRequested => _connectionChange;

    /// A keyboard has no face buttons to report. Its functions come out of FunctionRequested by name,
    /// which is what lets it carry more than the four a pad has room for.
    public IObservable<ButtonDirection> SpecialFunctionRequested => Observable.Never<ButtonDirection>();

    /// A key bound straight to a mixer input, which is the room a pad does not have.
    public IObservable<int> InputRequested => _input;

    /// The name of a function in the interface's `functions`, rather than the direction a pad would
    /// report, because a keyboard is not limited to four of them.
    public IObservable<string> FunctionRequested => _function;

    public IObservable<MixerTransition> TransitionRequested => _transition;

    public IObservable<AltKeyConfiguration> Modifiers => _modifiers;

    /// Nothing draws a keyboard, so this carries only what the two pads on screen show.
    public IObservable<GamepadState> State => _state;

    /// Pan on X, tilt on Y, both in [-1 .. 1].
    public void Move(double pan, double tilt)
    {
        var position = new StickPosition(pan, tilt);
        _state.OnNext(_state.Value with { LeftStick = position });
        _left.OnNext(position);
    }

    /// Focus on X, zoom on Y, both in [-1 .. 1].
    public void Lens(double focus, double zoom)
    {
        var position = new StickPosition(focus, zoom);
        _state.OnNext(_state.Value with { RightStick = position });
        _right.OnNext(position);
    }

    public void Select(ButtonDirection direction) => _connectionChange.OnNext(direction);

    public void SelectInput(int input) => _input.OnNext(input);

    public void Run(string function) => _function.OnNext(function);

    public void Transition(MixerTransition kind) => _transition.OnNext(kind);

    public void SetModifiers(bool alt, bool altLower)
    {
        var wanted = new AltKeyConfiguration(alt, altLower);
        if (!_modifiers.Value.Equals(wanted))
        {
            _modifiers.OnNext(wanted);
        }
    }

    public void Rumble(double intensity, TimeSpan duration)
    {
        // Nothing to shake.
    }

    public ValueTask DisposeAsync()
    {
        _left.Dispose();
        _right.Dispose();
        _connectionChange.Dispose();
        _input.Dispose();
        _function.Dispose();
        _transition.Dispose();
        _modifiers.Dispose();
        _state.Dispose();
        _connected.Dispose();
        return ValueTask.CompletedTask;
    }
}
