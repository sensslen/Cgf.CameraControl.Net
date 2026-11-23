using Avalonia.Controls;
using Avalonia.Data.Converters;
using Cgf.CameraControl.App.Scenes;

namespace Cgf.CameraControl.App.Views;

/// The scene wizard's two steps. SceneWizardWindow hosts it as a dialog.
public partial class SceneWizardView : UserControl
{
    /// A camera the scene cannot store is listed, dimmed, with the reason.
    public static readonly IValueConverter Dimmed = new FuncValueConverter<bool, double>(enabled => enabled ? 1 : 0.5);

    private double _zoom;
    private double _focus;

    public SceneWizardView()
    {
        InitializeComponent();

        MovePad.Moved += (pan, tilt) => Model?.SelectedCamera?.Move(pan, tilt);
        ZoomSlider.Moved += (zoom, _) => Model?.SelectedCamera?.Lens(_zoom = zoom, _focus);
        FocusSlider.Moved += (focus, _) => Model?.SelectedCamera?.Lens(_zoom, _focus = focus);
        GoToStored.Click += (_, _) => Model?.SelectedCamera?.GoToStored();
    }

    private SceneWizardViewModel? Model => DataContext as SceneWizardViewModel;
}
