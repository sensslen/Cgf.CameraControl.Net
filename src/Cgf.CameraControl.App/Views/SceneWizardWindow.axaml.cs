using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Cgf.CameraControl.App.Scenes;

namespace Cgf.CameraControl.App.Views;

/// The scene wizard as a modal dialog. It closes once the wizard finishes, and closing it any other
/// way, with Escape or the close button, cancels: nothing is stored. It stays open while the scene
/// is being stored.
public partial class SceneWizardWindow : Window
{
    public SceneWizardWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        };
        Closing += (_, e) => e.Cancel = DataContext is SceneWizardViewModel { IsStoring: true };
        Closed += (_, _) => (DataContext as SceneWizardViewModel)?.Cancel();
    }

    public static async Task ShowAsync(
        Window owner,
        SceneWizardViewModel model,
        Func<Window, string, Task<bool>> confirmDelete)
    {
        var window = new SceneWizardWindow { DataContext = model };
        model.ConfirmDelete = scene => confirmDelete(window, scene);
        _ = model.Completion.ContinueWith(
            _ => Dispatcher.UIThread.Post(window.Close),
            TaskScheduler.Default);
        await window.ShowDialog(owner).ConfigureAwait(true);
    }
}
