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

            _host = new AppHost();
            _model = new MainViewModel(_host);

            var window = new MainWindow { DataContext = _model };
            _model.PickFile = window.PickFileAsync;
            SystemMenu.Attach(window, _model);
            desktop.MainWindow = window;
            desktop.ShutdownRequested += OnShutdownRequested;

            var startup = AppEnvironment.ConfigFile?.FullName ?? settings.ConfigPath;
            if (startup is not null)
            {
                _ = _model.LoadAsync(startup);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _model?.Dispose();
        _host?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
