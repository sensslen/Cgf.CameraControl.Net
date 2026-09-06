namespace Cgf.CameraControl.App.ViewModels;

/// What the input a control selects is doing right now. A direction that would put the live camera
/// on preview is worth seeing before it is pressed, which is the whole reason a vision mixer colours
/// its rows rather than listing them.
public enum InputRole
{
    None,
    Preview,
    Program,
}
