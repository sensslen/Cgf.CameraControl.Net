namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

public readonly record struct StickPosition(double X, double Y);

public enum MixerTransition
{
    Cut,
    Auto,
}

/// The controls a wireframe draws. Only what an interface actually reads is here: a part of the pad
/// that can never light up is a bug report waiting to happen.
[Flags]
public enum GamepadButtons
{
    None = 0,
    DPadUp = 1 << 0,
    DPadDown = 1 << 1,
    DPadLeft = 1 << 2,
    DPadRight = 1 << 3,
    FaceUp = 1 << 4,
    FaceDown = 1 << 5,
    FaceLeft = 1 << 6,
    FaceRight = 1 << 7,
    LeftShoulder = 1 << 8,
    RightShoulder = 1 << 9,
}

/// Where every control is right now, as opposed to the events an interface acts on. A drawing needs
/// the release as much as the press, and the events carry only the press.
public readonly record struct GamepadState(
    StickPosition LeftStick,
    StickPosition RightStick,
    double LeftTrigger,
    double RightTrigger,
    GamepadButtons Pressed)
{
    public bool IsPressed(GamepadButtons button) => (Pressed & button) != 0;
}

/// A pad reduced to what the control surface actually uses. The TypeScript build had one class per
/// controller model because node-gamepad reported raw HID; SDL normalises the layout, so the button
/// mapping lives in one place and everything above this interface is model independent.
public interface IGamepadDevice : IAsyncDisposable
{
    string Description { get; }

    bool SupportsRumble { get; }

    IObservable<bool> WhenConnectedChanged { get; }

    /// Pan on X, tilt on Y. Both sticks report in camera terms rather than in SDL's, because the
    /// inversions belong with the pad they compensate for and nowhere above it.
    IObservable<StickPosition> LeftStick { get; }

    /// Focus on X, zoom on Y.
    IObservable<StickPosition> RightStick { get; }

    /// The direction pad.
    IObservable<ButtonDirection> ConnectionChangeRequested { get; }

    /// The face buttons, reported as the direction they sit in.
    IObservable<ButtonDirection> SpecialFunctionRequested { get; }

    IObservable<MixerTransition> TransitionRequested { get; }

    IObservable<AltKeyConfiguration> Modifiers { get; }

    /// What the window draws. Separate from the events above because those are what the interface
    /// acts on, and a control released is not something any of them report.
    IObservable<GamepadState> State { get; }

    void Rumble(double intensity, TimeSpan duration);
}
