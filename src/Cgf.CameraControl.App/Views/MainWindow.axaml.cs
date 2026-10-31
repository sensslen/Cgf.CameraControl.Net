using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using Cgf.CameraControl.App.ViewModels;

namespace Cgf.CameraControl.App.Views;

public partial class MainWindow : Window
{
    private const int TitleBarHeight = 40;

    /// What macOS leaves above the client area for its own buttons.
    private const int MacTrafficLightHeight = 28;

    // Built per dialog so the filter name follows the language chosen at run time.
    private static FilePickerFileType ConfigurationFiles => new(Localization.Localizer.Current.Text("config.fileType"))
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };

    private readonly InterfaceKeyboard _keyboard = new();

    public MainWindow()
    {
        InitializeComponent();

        // The selection decides what the keyboard drives, and losing the window with a key down
        // means no key up ever arrives for it.
        DataContextChanged += (_, _) => Follow();
        Deactivated += (_, _) => _keyboard.Release();

        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Source is not MenuItem)
            {
                e.Handled |= _keyboard.Down(e.Key);
            }
        }, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, (_, e) => e.Handled |= _keyboard.Up(e.Key), RoutingStrategies.Tunnel);

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
            Title = Localization.Localizer.Current.Text("config.openTitle"),
            AllowMultiple = false,
            FileTypeFilter = [ConfigurationFiles],
        });

        return chosen.Count == 0 ? null : chosen[0].TryGetLocalPath();
    }

    /// Where a configuration that has never had a path is written.
    public async Task<string?> PickSaveFileAsync()
    {
        var chosen = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Localization.Localizer.Current.Text("edit.saveAsTitle"),
            SuggestedFileName = "config.json",
            DefaultExtension = "json",
            FileTypeChoices = [ConfigurationFiles],
        });

        return chosen?.TryGetLocalPath();
    }

    private void Follow()
    {
        if (DataContext is not MainViewModel model)
        {
            return;
        }

        _keyboard.Target = model.SelectedInterface;
        model.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(MainViewModel.SelectedInterface))
            {
                _keyboard.Target = model.SelectedInterface;
            }
        };
    }
}
