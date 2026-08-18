namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

public interface IConnectionChange
{
    /// The input to select, or null when the direction is not bound.
    int? Next(ButtonDirection direction, int currentSelection, AltKeyConfiguration altKeys);
}
