using Avalonia.Controls;
using Cgf.CameraControl.App.ViewModels;

namespace Cgf.CameraControl.App.Views;

/// The keyboard and mouse half of an interface: the two pads a mouse drives, and one button per
/// thing the configuration bound. A pad interface never shows this, because a pad is not driven from
/// the window.
public partial class ControlSurfaceView : UserControl
{
    public ControlSurfaceView()
    {
        InitializeComponent();

        MovePad.Moved += (pan, tilt) => Surface?.Move(pan, tilt);
        LensPad.Moved += (focus, zoom) => Surface?.Lens(focus, zoom);
    }

    private ControlSurfaceViewModel? Surface => (DataContext as InterfaceViewModel)?.Surface;
}
