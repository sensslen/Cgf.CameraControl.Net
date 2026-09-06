using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Cgf.CameraControl.App.Hosting;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.App.ViewModels;
using Cgf.CameraControl.App.Views;

namespace Cgf.CameraControl.App;

public partial class App : Application
{
    private AppHost? _host;
    private MainViewModel? _model;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = Settings.Read();
            Localizer.Current.Use(settings.Language is { } chosen
                ? Localizer.Languages.FirstOrDefault(language => language.Culture == chosen)
                  ?? Localizer.Match(CultureInfo.CurrentUICulture)
                : Localizer.Match(CultureInfo.CurrentUICulture));

            // The name macOS puts in its menu bar, read before the menu is built. A language change
            // afterwards writes it again, which the next start picks up whether or not the running
            // menu bar does.
            Name = Localizer.Current.Text("app.title");
            Localizer.Current.LanguageChanged += (_, _) => Name = Localizer.Current.Text("app.title");

            _host = new AppHost();
            _model = new MainViewModel(_host);

            var window = new MainWindow { DataContext = _model };
            _model.PickFile = window.PickFileAsync;
            _model.PickSaveFile = window.PickSaveFileAsync;
            _model.ShowLicenses = () => new LicensesWindow().ShowDialog(window);
            _model.AskToSave = (canSave, issues) => AskWindow.LeavingAsync(window, canSave, issues);
            _model.AskToDelete = (entry, references) => AskWindow.DeletingAsync(window, entry, references);
            _model.AskToAdd = entry => AddEntryWindow.AskAsync(window, entry);
            SystemMenu.Attach(window, _model);
            desktop.MainWindow = window;
            desktop.ShutdownRequested += OnShutdownRequested;

            // With nothing to run, the window would be a menu over an empty panel. Edit mode is what
            // it is for: the configuration that does not exist yet is made here.
            var startup = AppEnvironment.ConfigFile?.FullName ?? settings.ConfigPath;
            _ = startup is not null ? _model.LoadAsync(startup) : _model.BeginEditingAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _model?.Dispose();
        _host?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
