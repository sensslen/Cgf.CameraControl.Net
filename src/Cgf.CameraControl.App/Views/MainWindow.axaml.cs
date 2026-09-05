using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace Cgf.CameraControl.App.Views;

public partial class MainWindow : Window
{
    private const int TitleBarHeight = 40;

    /// What macOS leaves above the client area for its own buttons.
    private const int MacTrafficLightHeight = 28;

    private static readonly FilePickerFileType ConfigurationFiles = new("Configuration")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };

    public MainWindow()
    {
        InitializeComponent();

        // Windows and Linux let Avalonia draw the frame, which is what makes an icon, a name and a
        // menu inside the title bar possible at all. macOS draws its own traffic lights and takes the
        // menu into the system menu bar, so there is nothing of ours to put up there, but the client
        // area is extended under it all the same: a title bar the window does not own is opaque, and
        // the window would wear a solid strip above a backdrop that is not.
        var drawsOwnTitleBar = !OperatingSystem.IsMacOS();
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaTitleBarHeightHint = drawsOwnTitleBar ? TitleBarHeight : -1;
        CaptionStrip.IsVisible = drawsOwnTitleBar;

        if (!drawsOwnTitleBar)
        {
            // The traffic lights now sit over the top left of the client area, so nothing else may.
            Root.Margin = new Thickness(0, MacTrafficLightHeight, 0, 0);

            // The drawn decorations are the Windows and Linux frame. macOS has its own.
            WindowDecorationsTheme = null;
        }
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
