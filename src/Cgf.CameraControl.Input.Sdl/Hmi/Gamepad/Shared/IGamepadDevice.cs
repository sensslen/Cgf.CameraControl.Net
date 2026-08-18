namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

public readonly record struct StickPosition(double X, double Y);

public enum MixerTransition
{
    Cut,
    Auto,
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

    void Rumble(double intensity, TimeSpan duration);
}
