using System.Collections.ObjectModel;
using Cgf.CameraControl.App.Hosting;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Hmi;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cgf.CameraControl.App.ViewModels;

public sealed partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly AppHost _host;

    public MainViewModel(AppHost host)
    {
        _host = host;
        Log = new LogViewModel(host.Logger);
        Languages = [.. Localizer.Languages.Select(language => new LanguageViewModel(language))];
        Localizer.Current.LanguageChanged += OnLanguageChanged;
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

    /// Supplied by the window, because opening one needs an owner and a view model must not hold it.
    public Func<InterfaceViewModel, Task>? ShowInterface { get; set; }

    public IReadOnlyList<MixerViewModel> UnassignedMixers { get; private set; } = [];

    public IReadOnlyList<CameraViewModel> UnassignedCameras { get; private set; } = [];

    public bool HasUnassigned => UnassignedMixers.Count > 0 || UnassignedCameras.Count > 0;

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

        var mixerViews = _host.Core.MixerFactory.Instances
            .OrderBy(entry => entry.Key)
            .Zip(Mixers, (entry, view) => (Mixer: entry.Value, View: view))
            .ToDictionary(pair => pair.Mixer, pair => pair.View);
        var cameraViews = _host.Cameras
            .Zip(Cameras, (camera, view) => (Camera: (ICameraConnection)camera, View: view))
            .ToDictionary(pair => pair.Camera, pair => pair.View);

        foreach (var (instance, hmi) in _host.Core.HmiFactory.Instances.OrderBy(entry => entry.Key))
        {
            var device = _host.Surfaces.FirstOrDefault(surface => surface.Instance == instance);
            Interfaces.Add(new InterfaceViewModel(
                instance,
                hmi,
                device is null ? null : new ControlSurfaceViewModel(device),
                Owned(hmi, mixerViews, gamepad => [gamepad.Mixer]),
                Owned(hmi, cameraViews, gamepad => gamepad.Cameras))
            {
                Open = ShowInterface,
            });
        }

        // Anything no interface claims would otherwise not be drawn at all, and a camera missing
        // from the panel reads as one that failed to load rather than one nothing can reach.
        UnassignedMixers = [.. Mixers.Where(view => Interfaces.All(entry => !entry.Mixers.Contains(view)))];
        UnassignedCameras = [.. Cameras.Where(view => Interfaces.All(entry => !entry.Cameras.Contains(view)))];

        OnPropertyChanged(nameof(UnassignedMixers));
        OnPropertyChanged(nameof(UnassignedCameras));
        OnPropertyChanged(nameof(HasUnassigned));
    }

    /// The interface knows what it resolved, which is not the same as what its configuration asked
    /// for: a camera that failed to load is not one this interface drives.
    private static IReadOnlyList<TView> Owned<TModel, TView>(
        IHmi hmi,
        Dictionary<TModel, TView> views,
        Func<Gamepad, IEnumerable<TModel>> owned)
        where TModel : notnull =>
        hmi is Gamepad gamepad
            ? [.. owned(gamepad).Select(model => views.GetValueOrDefault(model)).OfType<TView>()]
            : [];

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
