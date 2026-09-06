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

        // The lens takes both axes at once, and one slider only knows its own, so the other's last
        // position rides along; a mouse can only be on one of them anyway.
        ZoomSlider.Moved += (zoom, _) => Surface?.Lens(_focus, _zoom = zoom);
        FocusSlider.Moved += (focus, _) => Surface?.Lens(_focus = focus, _zoom);
    }

    private double _focus;
    private double _zoom;

    private ControlSurfaceViewModel? Surface => (DataContext as InterfaceViewModel)?.Surface;
}
