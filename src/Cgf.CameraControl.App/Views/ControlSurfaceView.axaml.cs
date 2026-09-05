using Avalonia.Controls;
using Cgf.CameraControl.App.ViewModels;

namespace Cgf.CameraControl.App.Views;

public partial class ControlSurfaceView : UserControl
{
    public ControlSurfaceView()
    {
        InitializeComponent();

        MovePad.Moved += (pan, tilt) => Surface?.Move(pan, tilt);
        LensPad.Moved += (focus, zoom) => Surface?.Lens(focus, zoom);
    }

    private ControlSurfaceViewModel? Surface => DataContext as ControlSurfaceViewModel;
}
