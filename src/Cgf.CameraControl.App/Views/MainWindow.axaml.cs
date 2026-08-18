using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Cgf.CameraControl.App.Views;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType ConfigurationFiles = new("Configuration")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };

    public MainWindow() => InitializeComponent();

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
}
