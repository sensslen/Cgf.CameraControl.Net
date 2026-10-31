using System.Globalization;
using Avalonia;
using Avalonia.Controls;
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
    private Task _startup = Task.CompletedTask;
    private Task? _closing;
    private Task? _release;
    private bool _released;

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
            desktop.MainWindow = window;
            window.Closing += OnClosing;
            desktop.Exit += OnExit;

            // With nothing to run, the window would be a menu over an empty panel. Edit mode is what
            // it is for: the configuration that does not exist yet is made here.
            var startup = AppEnvironment.ConfigFile?.FullName ?? settings.ConfigPath;
            _startup = ObserveAsync("startup", startup is not null ? _model.LoadAsync(startup) : _model.BeginEditingAsync());
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// The first close is refused and finished asynchronously: unsaved edits are offered for saving,
    /// and the devices are taken down only after the startup load is done with them. Blocking here
    /// instead would hold the UI thread the load needs to finish on.
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_released || sender is not Window window)
        {
            return;
        }

        e.Cancel = true;
        _closing ??= ObserveAsync("closing", CloseAsync(window));
    }

    /// An event handler cannot await what it starts, so whatever it starts reports its own failure.
    private async Task ObserveAsync(string what, Task task)
    {
        try
        {
            await task.ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _host?.Logger.Error("Application", $"{what} failed: {ex.Message}");
        }
    }

    private async Task CloseAsync(Window window)
    {
        try
        {
            if (_model is not null && !await _model.CanCloseAsync().ConfigureAwait(true))
            {
                return;
            }

            await _startup.ConfigureAwait(true);
            await ReleaseAsync().ConfigureAwait(true);
            _released = true;
            window.Close();
        }
        finally
        {
            _closing = null;
        }
    }

    /// Once, whichever of the close or a forced exit gets here first, and both wait for that one.
    private Task ReleaseAsync() => _release ??= ReleaseOnceAsync();

    private async Task ReleaseOnceAsync()
    {
        _model?.Dispose();
        _model = null;
        if (_host is { } host)
        {
            _host = null;
            await host.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// A forced shutdown closes the window without waiting for the close above. The host disposes
    /// without the UI thread, so blocking on it here cannot deadlock.
    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e) =>
        ReleaseAsync().GetAwaiter().GetResult();
}
