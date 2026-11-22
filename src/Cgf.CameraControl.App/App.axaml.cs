using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Cgf.CameraControl.App.Hosting;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.App.ViewModels;
using Cgf.CameraControl.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Cgf.CameraControl.App;

public partial class App : Application
{
    private ServiceProvider? _services;
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
            _services = new ServiceCollection().AddCommonServices().BuildServiceProvider();

            var settings = Settings.Read();
            var strings = _services.GetRequiredService<ILocalizer>();
            strings.Use(settings.Language is { } chosen
                ? Localizer.Languages.FirstOrDefault(language => language.Culture == chosen)
                  ?? Localizer.Match(CultureInfo.CurrentUICulture)
                : Localizer.Match(CultureInfo.CurrentUICulture));

            // The name macOS puts in its menu bar, read before the menu is built. A language change
            // afterwards writes it again, which the next start picks up whether or not the running
            // menu bar does.
            Name = strings.Text("app.title");
            strings.LanguageChanged += (_, _) => Name = strings.Text("app.title");

            _host = _services.GetRequiredService<AppHost>();
            _model = _services.GetRequiredService<MainViewModel>();

            var window = new MainWindow { DataContext = _model };
            _model.PickFile = window.PickFileAsync;
            _model.PickSaveFile = window.PickSaveFileAsync;
            _model.ShowLicenses = () => new LicensesWindow { DataContext = strings }.ShowDialog(window);
            _model.AskToSave = (canSave, issues) => AskWindow.LeavingAsync(window, strings, canSave, issues);
            _model.AskToDelete = (entry, references) => AskWindow.DeletingAsync(window, strings, entry, references);
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

    /// The container disposes in reverse order of creation, so the view model lets go of the devices
    /// before the host takes them down.
    private async Task ReleaseOnceAsync()
    {
        _model = null;
        // The host stays set after disposal so a teardown failure can still be logged through it.
        if (_services is { } services)
        {
            await services.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// A forced shutdown closes the window without waiting for the close above. The host disposes
    /// without the UI thread, so blocking on it here cannot deadlock.
    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e) =>
        ReleaseAsync().GetAwaiter().GetResult();
}
