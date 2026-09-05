using System.Collections.ObjectModel;
using Cgf.CameraControl.App.Hosting;
using Cgf.CameraControl.App.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cgf.CameraControl.App.ViewModels;

public sealed partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly AppHost _host;
    private readonly IDisposable _presence;

    public MainViewModel(AppHost host)
    {
        _host = host;
        Log = new LogViewModel(host.Logger);
        Languages = [.. Localizer.Languages.Select(language => new LanguageViewModel(language))];
        Localizer.Current.LanguageChanged += OnLanguageChanged;
        _presence = host.Gamepads.WhenPresenceChanged.Bind(pads =>
        {
            Gamepads.Clear();
            foreach (var pad in pads)
            {
                Gamepads.Add(new GamepadViewModel(pad));
            }
        });
    }

    /// Supplied by the window, because a file dialog needs one and a view model must not hold it.
    public Func<bool, Task<string?>>? PickFile { get; set; }

    /// Supplied by the window for the same reason: a modal dialog needs an owner.
    public Func<Task>? ShowLicenses { get; set; }

    public Localizer Strings => Localizer.Current;

    public IReadOnlyList<LanguageViewModel> Languages { get; }

    public LogViewModel Log { get; }

    public ObservableCollection<MixerViewModel> Mixers { get; } = [];

    public ObservableCollection<CameraViewModel> Cameras { get; } = [];

    public ObservableCollection<InterfaceViewModel> Interfaces { get; } = [];

    public ObservableCollection<GamepadViewModel> Gamepads { get; } = [];

    /// Reported per entry rather than as one failed load, so a typo in one camera does not hide the
    /// nine that are fine.
    public ObservableCollection<string> Issues { get; } = [];

    [ObservableProperty]
    public partial string ConfigPath { get; set; } = "no configuration loaded";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public async Task LoadAsync(string path)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _host.LoadAsync(path, CancellationToken.None).ConfigureAwait(true);
            ConfigPath = result.Path;

            Issues.Clear();
            foreach (var issue in result.FileIssues)
            {
                Issues.Add(issue);
            }

            foreach (var issue in result.EntryIssues)
            {
                Issues.Add(issue.ToString());
            }

            Rebuild();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Dispose()
    {
        Localizer.Current.LanguageChanged -= OnLanguageChanged;
        _presence.Dispose();
        Log.Dispose();
        Clear();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var language in Languages)
        {
            language.IsActive = Localizer.Current.Active == language.Language;
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (PickFile is { } pick && await pick(false).ConfigureAwait(true) is { } path)
        {
            await LoadAsync(path).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (PickFile is { } pick && await pick(true).ConfigureAwait(true) is { } path)
        {
            await _host.ExportAsync(path, CancellationToken.None).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task ShowLicensesAsync()
    {
        if (ShowLicenses is { } show)
        {
            await show().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        if (_host.ConfigPath is { } path)
        {
            await LoadAsync(path).ConfigureAwait(true);
        }
    }

    private void Rebuild()
    {
        Clear();

        foreach (var (instance, mixer) in _host.Core.MixerFactory.Instances.OrderBy(entry => entry.Key))
        {
            Mixers.Add(new MixerViewModel(instance, mixer));
        }

        foreach (var camera in _host.Cameras)
        {
            Cameras.Add(new CameraViewModel(camera));
        }

        foreach (var (instance, hmi) in _host.Core.HmiFactory.Instances.OrderBy(entry => entry.Key))
        {
            Interfaces.Add(new InterfaceViewModel(instance, hmi));
        }
    }

    private void Clear()
    {
        foreach (var mixer in Mixers)
        {
            mixer.Dispose();
        }

        foreach (var camera in Cameras)
        {
            camera.Dispose();
        }

        foreach (var hmi in Interfaces)
        {
            hmi.Dispose();
        }

        Mixers.Clear();
        Cameras.Clear();
        Interfaces.Clear();
    }
}
