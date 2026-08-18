using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace Cgf.CameraControl.App.Views;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType ConfigurationFiles = new("Configuration")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };

    public MainWindow()
    {
        InitializeComponent();

        // On macOS the menu is published to the system menu bar instead, and a second copy inside
        // the window would be both wrong and redundant.
        InWindowMenu.IsVisible = !OperatingSystem.IsMacOS();
    }

    public async Task<string?> PickFileAsync(bool save)
    {
        if (save)
        {
            var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export configuration",
                SuggestedFileName = "config.json",
                DefaultExtension = "json",
                FileTypeChoices = [ConfigurationFiles],
            });
            return target?.TryGetLocalPath();
        }

        var chosen = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open configuration",
            AllowMultiple = false,
            FileTypeFilter = [ConfigurationFiles],
        });

        return chosen.Count == 0 ? null : chosen[0].TryGetLocalPath();
    }

    /// The backdrop is a request, not a guarantee: an older Windows, a Linux compositor without
    /// blur, or a machine with transparency effects switched off all refuse it. A transparent
    /// background over a refused backdrop is a see-through window, so paint one when that happens.
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ActualTransparencyLevelProperty)
        {
            Background = ActualTransparencyLevel == WindowTransparencyLevel.None
                ? this.FindResource("SystemControlBackgroundAltHighBrush") as IBrush
                : Brushes.Transparent;
        }
    }
}
