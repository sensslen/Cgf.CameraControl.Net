namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

public readonly record struct AltKeyConfiguration(bool Alt, bool AltLower)
{
    public static AltKeyConfiguration None { get; }
}
