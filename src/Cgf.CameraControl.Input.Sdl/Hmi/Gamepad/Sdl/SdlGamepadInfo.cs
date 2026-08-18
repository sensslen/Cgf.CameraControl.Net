namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;

public sealed record SdlGamepadInfo(uint InstanceId, string Name, string? Serial, string Path, bool SupportsRumble)
{
    /// The serial is what a configuration file can name, but not every pad reports one, so the OS
    /// device path stands in for the UI's device picker.
    public string Identity => Serial ?? Path;
}

public sealed record SdlGamepadPresence(SdlGamepadInfo Info, string? ClaimedBy);
