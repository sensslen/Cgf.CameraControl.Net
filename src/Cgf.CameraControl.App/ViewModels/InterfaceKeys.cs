using Avalonia.Input;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.App.ViewModels;

/// The bindings that are not buttons: an axis is a key held open rather than a thing to press, so it
/// has no control on the panel to light up.
///
/// Null means the operator bound nothing, which is not the same as binding it to something that does
/// nothing: an unbound key is one the window ignores entirely.
public sealed record InterfaceKeys(
    Key? PanLeft,
    Key? PanRight,
    Key? TiltUp,
    Key? TiltDown,
    Key? ZoomIn,
    Key? ZoomOut,
    Key? FocusFar,
    Key? FocusNear,
    IReadOnlyDictionary<Key, ButtonDirection> ConnectionChange)
{
    /// What a pad interface carries, because a pad is not driven from the window.
    public static InterfaceKeys None { get; } = new(
        null, null, null, null, null, null, null, null,
        new Dictionary<Key, ButtonDirection>());
}
