using System.Reactive.Subjects;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.Input.Sdl.Tests;

public sealed class FakeGamepadDevice : IGamepadDevice
{
    private readonly BehaviorSubject<bool> _connected = new(true);
    private readonly Subject<StickPosition> _left = new();
    private readonly Subject<StickPosition> _right = new();
    private readonly Subject<ButtonDirection> _dpad = new();
    private readonly Subject<ButtonDirection> _face = new();
    private readonly Subject<MixerTransition> _transition = new();
    private readonly BehaviorSubject<AltKeyConfiguration> _modifiers = new(AltKeyConfiguration.None);

    public string Description => "fake pad";

    public bool SupportsRumble { get; set; } = true;

    public List<(double Intensity, TimeSpan Duration)> Rumbles { get; } = [];

    public IObservable<bool> WhenConnectedChanged => _connected;

    public IObservable<StickPosition> LeftStick => _left;

    public IObservable<StickPosition> RightStick => _right;

    public IObservable<ButtonDirection> ConnectionChangeRequested => _dpad;

    public IObservable<ButtonDirection> SpecialFunctionRequested => _face;

    public IObservable<MixerTransition> TransitionRequested => _transition;

    public IObservable<AltKeyConfiguration> Modifiers => _modifiers;

    public void MoveLeftStick(double x, double y) => _left.OnNext(new StickPosition(x, y));

    public void MoveRightStick(double x, double y) => _right.OnNext(new StickPosition(x, y));

    public void PressDirection(ButtonDirection direction) => _dpad.OnNext(direction);

    public void PressFace(ButtonDirection direction) => _face.OnNext(direction);

    public void RequestTransition(MixerTransition transition) => _transition.OnNext(transition);

    public void HoldAlt() => _modifiers.OnNext(new AltKeyConfiguration(Alt: true, AltLower: false));

    public void HoldAltLower() => _modifiers.OnNext(new AltKeyConfiguration(Alt: false, AltLower: true));

    public void ReleaseModifiers() => _modifiers.OnNext(AltKeyConfiguration.None);

    public void Rumble(double intensity, TimeSpan duration) => Rumbles.Add((intensity, duration));

    public ValueTask DisposeAsync()
    {
        _connected.Dispose();
        _left.Dispose();
        _right.Dispose();
        _dpad.Dispose();
        _face.Dispose();
        _transition.Dispose();
        _modifiers.Dispose();
        return ValueTask.CompletedTask;
    }
}
