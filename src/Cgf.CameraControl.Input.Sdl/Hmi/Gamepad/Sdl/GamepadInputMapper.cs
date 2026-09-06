using System.Reactive.Subjects;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using SDL3;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;

/// Translates SDL's normalised axes and buttons into the control surface. This is the only place
/// that knows a pad exists: the TypeScript build needed a class per controller model because
/// node-gamepad reported raw HID, and SDL's mapping database removes that entirely.
///
/// The bindings match the F310 and Rumblepad 2 they replace. Every face button is bound even though
/// the F310 class left the north button unused, because the Rumblepad 2 class bound all four and
/// SDL reports them the same way.
public sealed class GamepadInputMapper : IDisposable
{
    private const double TriggerPress = 0.6;
    private const double TriggerRelease = 0.4;
    private const double AxisRange = 32767.0;

    private readonly double _deadzone;
    private readonly Subject<StickPosition> _leftStick = new();
    private readonly Subject<StickPosition> _rightStick = new();
    private readonly Subject<ButtonDirection> _connectionChange = new();
    private readonly Subject<ButtonDirection> _specialFunction = new();
    private readonly Subject<MixerTransition> _transition = new();
    private readonly BehaviorSubject<AltKeyConfiguration> _modifiers = new(AltKeyConfiguration.None);
    private readonly BehaviorSubject<GamepadState> _state = new(default);

    private StickPosition _left;
    private StickPosition _right;
    private bool _alt;
    private bool _altLower;
    private bool _leftTrigger;
    private bool _rightTrigger;

    public GamepadInputMapper(double deadzone) => _deadzone = deadzone;

    public IObservable<StickPosition> LeftStick => _leftStick;

    public IObservable<StickPosition> RightStick => _rightStick;

    public IObservable<ButtonDirection> ConnectionChangeRequested => _connectionChange;

    public IObservable<ButtonDirection> SpecialFunctionRequested => _specialFunction;

    public IObservable<MixerTransition> TransitionRequested => _transition;

    public IObservable<AltKeyConfiguration> Modifiers => _modifiers;

    public IObservable<GamepadState> State => _state;

    public void Axis(SDL.GamepadAxis axis, short raw)
    {
        switch (axis)
        {
            case SDL.GamepadAxis.LeftX:
                SetLeft(_left with { X = -Stick(raw) });
                break;
            case SDL.GamepadAxis.LeftY:
                SetLeft(_left with { Y = Stick(raw) });
                break;
            case SDL.GamepadAxis.RightX:
                SetRight(_right with { X = -Stick(raw) });
                break;
            case SDL.GamepadAxis.RightY:
                SetRight(_right with { Y = -Stick(raw) });
                break;
            case SDL.GamepadAxis.LeftTrigger:
                _leftTrigger = Gate(_leftTrigger, raw);
                Draw(state => state with
                {
                    LeftTrigger = Travel(raw),
                    Pressed = Held(state.Pressed, GamepadButtons.LeftTrigger, _leftTrigger),
                });
                SetModifiers(_alt, _leftTrigger);
                break;
            case SDL.GamepadAxis.RightTrigger:
                var pressed = Gate(_rightTrigger, raw);
                if (pressed && !_rightTrigger)
                {
                    _transition.OnNext(MixerTransition.Auto);
                }

                _rightTrigger = pressed;
                Draw(state => state with
                {
                    RightTrigger = Travel(raw),
                    Pressed = Held(state.Pressed, GamepadButtons.RightTrigger, pressed),
                });
                break;
        }
    }

    public void Button(SDL.GamepadButton button, bool down)
    {
        // The events below fire on the press alone, because pressing is what an interface acts on.
        // A drawing needs the release too, so the held picture is kept beside them rather than
        // rebuilt from events that never report one.
        if (Drawn(button) is { } drawn)
        {
            Draw(state => state with { Pressed = Held(state.Pressed, drawn, down) });
        }

        switch (button)
        {
            case SDL.GamepadButton.DPadUp when down:
                _connectionChange.OnNext(ButtonDirection.Up);
                break;
            case SDL.GamepadButton.DPadDown when down:
                _connectionChange.OnNext(ButtonDirection.Down);
                break;
            case SDL.GamepadButton.DPadLeft when down:
                _connectionChange.OnNext(ButtonDirection.Left);
                break;
            case SDL.GamepadButton.DPadRight when down:
                _connectionChange.OnNext(ButtonDirection.Right);
                break;
            case SDL.GamepadButton.North when down:
                _specialFunction.OnNext(ButtonDirection.Up);
                break;
            case SDL.GamepadButton.South when down:
                _specialFunction.OnNext(ButtonDirection.Down);
                break;
            case SDL.GamepadButton.West when down:
                _specialFunction.OnNext(ButtonDirection.Left);
                break;
            case SDL.GamepadButton.East when down:
                _specialFunction.OnNext(ButtonDirection.Right);
                break;
            case SDL.GamepadButton.RightShoulder when down:
                _transition.OnNext(MixerTransition.Cut);
                break;
            case SDL.GamepadButton.LeftShoulder:
                SetModifiers(down, _altLower);
                break;
        }
    }

    /// A pad unplugged mid-move leaves the camera it was steering running, so the sticks are
    /// recentred rather than left at their last reported position.
    public void Reset()
    {
        _leftTrigger = false;
        _rightTrigger = false;
        _state.OnNext(default);
        SetLeft(default);
        SetRight(default);
        SetModifiers(false, false);
    }

    public void Dispose()
    {
        _leftStick.Dispose();
        _rightStick.Dispose();
        _connectionChange.Dispose();
        _specialFunction.Dispose();
        _transition.Dispose();
        _modifiers.Dispose();
        _state.Dispose();
    }

    private static double Normalize(short raw) => Math.Clamp(raw / AxisRange, -1, 1);

    /// A trigger rests at zero and only travels one way, so its drawn depth is the positive half.
    private static double Travel(short raw) => Math.Clamp(Normalize(raw), 0, 1);

    /// Null for a control no interface reads, which is what keeps the wireframe to parts that light.
    private static GamepadButtons? Drawn(SDL.GamepadButton button) => button switch
    {
        SDL.GamepadButton.DPadUp => GamepadButtons.DPadUp,
        SDL.GamepadButton.DPadDown => GamepadButtons.DPadDown,
        SDL.GamepadButton.DPadLeft => GamepadButtons.DPadLeft,
        SDL.GamepadButton.DPadRight => GamepadButtons.DPadRight,
        SDL.GamepadButton.North => GamepadButtons.FaceUp,
        SDL.GamepadButton.South => GamepadButtons.FaceDown,
        SDL.GamepadButton.West => GamepadButtons.FaceLeft,
        SDL.GamepadButton.East => GamepadButtons.FaceRight,
        SDL.GamepadButton.LeftShoulder => GamepadButtons.LeftShoulder,
        SDL.GamepadButton.RightShoulder => GamepadButtons.RightShoulder,
        _ => null,
    };

    private static GamepadButtons Held(GamepadButtons pressed, GamepadButtons button, bool down) =>
        down ? pressed | button : pressed & ~button;

    private void Draw(Func<GamepadState, GamepadState> change)
    {
        var next = change(_state.Value);
        if (next != _state.Value)
        {
            _state.OnNext(next);
        }
    }

    /// The F310 reports its triggers as full-travel axes, so a plain threshold would chatter between
    /// press and release while a finger rests on the edge of it.
    private static bool Gate(bool state, short raw)
    {
        var value = Normalize(raw);
        return state ? value > TriggerRelease : value >= TriggerPress;
    }

    /// The travel outside the deadzone is rescaled over the full range, so a stick crossing the
    /// threshold starts at zero speed instead of jumping to the deadzone width.
    private double Stick(short raw)
    {
        var value = Normalize(raw);
        var magnitude = Math.Abs(value);
        return magnitude <= _deadzone ? 0 : Math.Sign(value) * (magnitude - _deadzone) / (1 - _deadzone);
    }

    private void SetLeft(StickPosition position)
    {
        if (position == _left)
        {
            return;
        }

        _left = position;
        Draw(state => state with { LeftStick = position });
        _leftStick.OnNext(position);
    }

    private void SetRight(StickPosition position)
    {
        if (position == _right)
        {
            return;
        }

        _right = position;
        Draw(state => state with { RightStick = position });
        _rightStick.OnNext(position);
    }

    private void SetModifiers(bool alt, bool altLower)
    {
        if (alt == _alt && altLower == _altLower)
        {
            return;
        }

        _alt = alt;
        _altLower = altLower;
        _modifiers.OnNext(new AltKeyConfiguration(alt, altLower));
    }
}
