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

        // macOS draws its own title bar with the traffic lights in it and publishes the menu to the
        // system menu bar, so this window has nothing to draw up there and no client area to extend.
        // Windows and Linux let Avalonia draw the frame, which is what makes an icon, a name and a
        // menu inside the title bar possible at all.
        var drawsOwnTitleBar = !OperatingSystem.IsMacOS();
        ExtendClientAreaToDecorationsHint = drawsOwnTitleBar;
        ExtendClientAreaTitleBarHeightHint = 40;
        CaptionStrip.IsVisible = drawsOwnTitleBar;
    }

    public async Task<string?> PickFileAsync()
    {
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
