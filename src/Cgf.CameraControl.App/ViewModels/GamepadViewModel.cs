using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;

namespace Cgf.CameraControl.App.ViewModels;

/// A pad SDL currently reports. This is the device picker: a serial can be read off a connected pad
/// and typed into the configuration, instead of being hunted for in the operating system.
public sealed class GamepadViewModel(SdlGamepadPresence presence) : ViewModelBase
{
    public string Name => presence.Info.Name;

    public string Serial => presence.Info.Serial ?? "no serial reported";

    public string Path => presence.Info.Path;

    public bool SupportsRumble => presence.Info.SupportsRumble;

    public bool IsClaimed => presence.ClaimedBy is not null;

    public string ClaimedBy => presence.ClaimedBy ?? "not claimed by any interface";
}
