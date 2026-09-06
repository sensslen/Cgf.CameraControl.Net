using System.Collections.ObjectModel;
using Cgf.CameraControl.App.Editing;
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
        ConfigPath = Localizer.Current.Text("config.none");
        Localizer.Current.LanguageChanged += OnLanguageChanged;
    }

    /// Supplied by the window, because a file dialog needs one and a view model must not hold it.
    public Func<Task<string?>>? PickFile { get; set; }

    /// Supplied by the window for the same reason: a modal dialog needs an owner.
    public Func<Task>? ShowLicenses { get; set; }

    /// Where the configuration is written when it has never had a path.
    public Func<Task<string?>>? PickSaveFile { get; set; }

    /// Asked on the way out of edit mode. Save is offered only when nothing blocks it, which is what
    /// keeps a configuration naming something it does not define from reaching the disk.
    public Func<bool, IReadOnlyList<string>, Task<SaveChoice>>? AskToSave { get; set; }

    /// Asked before an entry is deleted, carrying whatever still names it.
    public Func<EntryDraft, IReadOnlyList<string>, Task<bool>>? AskToDelete { get; set; }

    /// The add wizard. Returns the finished entry, or nothing when it is cancelled, so an abandoned
    /// add leaves the configuration exactly as it was.
    public Func<NewEntry, Task<EntryDraft?>>? AskToAdd { get; set; }

    public Localizer Strings => Localizer.Current;

    public IReadOnlyList<LanguageViewModel> Languages { get; }

    public LogViewModel Log { get; }

    public ObservableCollection<MixerViewModel> Mixers { get; } = [];

    public ObservableCollection<CameraViewModel> Cameras { get; } = [];

    public ObservableCollection<InterfaceViewModel> Interfaces { get; } = [];

    /// The one the main area draws and the keyboard drives. One interface at a time, because a
    /// keystroke that moves three desks at once is not something an operator can take back.
    [ObservableProperty]
    public partial InterfaceViewModel? SelectedInterface { get; set; }

    public IReadOnlyList<MixerViewModel> UnassignedMixers { get; private set; } = [];

    public IReadOnlyList<CameraViewModel> UnassignedCameras { get; private set; } = [];

    public bool HasUnassigned => UnassignedMixers.Count > 0 || UnassignedCameras.Count > 0;

    /// The pane is collapsed by default, so its lamp is all an operator sees of what is inside it.
    /// Green means every one of them is up: anything less is worth opening the pane for, and one
    /// camera down among nine is exactly the case a lamp that averaged them would hide.
    public bool AllMixersConnected => Mixers.Count > 0 && Mixers.All(mixer => mixer.IsConnected);

    public bool AllCamerasConnected => Cameras.Count > 0 && Cameras.All(camera => camera.IsConnected);

    /// Reported per entry rather than as one failed load, so a typo in one camera does not hide the
    /// nine that are fine.
    public ObservableCollection<string> Issues { get; } = [];

    [ObservableProperty]
    public partial string ConfigPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// The configuration being edited, or nothing while the desk is running. Everything about edit
    /// mode hangs off this: the window reads it to know which half of itself to draw.
    [ObservableProperty]
    public partial ConfigDraft? Draft { get; set; }

    public bool IsEditing => Draft is not null;

    /// The file menu replaces the file being edited, so it is out of reach until the mode is left.
    public bool CanUseFileMenu => !IsBusy && !IsEditing;

    /// One selection across three lists, because one editor fills the area they all point at.
    [ObservableProperty]
    public partial EntryDraft? SelectedEntry { get; set; }

    [ObservableProperty]
    public partial EntryDraft? SelectedCamera { get; set; }

    [ObservableProperty]
    public partial EntryDraft? SelectedMixer { get; set; }

    [ObservableProperty]
    public partial EntryDraft? SelectedInterfaceEntry { get; set; }

    /// Entered by the application itself when it starts with no configuration to run, because a
    /// window with nothing in it but a menu is not an answer to having nothing configured.
    public async Task BeginEditingAsync()
    {
        if (IsEditing || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var configuration = _host.Configuration;
            await _host.UnloadAsync(CancellationToken.None).ConfigureAwait(true);
            Clear();
            Issues.Clear();
            Draft = ConfigDraft.From(configuration, ConnectedPads());
            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(CanUseFileMenu));
            SelectedInterfaceEntry = Draft.Interfaces.FirstOrDefault();
        }
        finally
        {
            IsBusy = false;
        }
    }

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

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanUseFileMenu));

    partial void OnSelectedCameraChanged(EntryDraft? value) => Choose(value, EntryKind.Camera);

    partial void OnSelectedMixerChanged(EntryDraft? value) => Choose(value, EntryKind.Mixer);

    partial void OnSelectedInterfaceEntryChanged(EntryDraft? value) => Choose(value, EntryKind.Interface);

    /// A list clears the other two when it takes the selection. Clearing is idempotent, so the
    /// notifications this sets off stop after one pass.
    private void Choose(EntryDraft? value, EntryKind kind)
    {
        if (value is null)
        {
            if (SelectedCamera is null && SelectedMixer is null && SelectedInterfaceEntry is null)
            {
                SelectedEntry = null;
            }

            return;
        }

        if (kind != EntryKind.Camera)
        {
            SelectedCamera = null;
        }

        if (kind != EntryKind.Mixer)
        {
            SelectedMixer = null;
        }

        if (kind != EntryKind.Interface)
        {
            SelectedInterfaceEntry = null;
        }

        SelectedEntry = value;
    }

    [RelayCommand]
    private async Task ToggleEditAsync()
    {
        if (Draft is null)
        {
            await BeginEditingAsync().ConfigureAwait(true);
            return;
        }

        await LeaveEditingAsync().ConfigureAwait(true);
    }

    private async Task LeaveEditingAsync()
    {
        if (Draft is not { } draft)
        {
            return;
        }

        if (draft.IsDirty && AskToSave is { } ask)
        {
            var choice = await ask(draft.CanSave, draft.Issues).ConfigureAwait(true);
            if (choice == SaveChoice.Cancel)
            {
                return;
            }

            if (choice == SaveChoice.Save)
            {
                await SaveAsync(draft).ConfigureAwait(true);
                return;
            }
        }

        await FinishEditingAsync(_host.ConfigPath).ConfigureAwait(true);
    }

    /// The file is loaded after it is written rather than the draft being adopted, so what the desk
    /// runs is what is on the disk, and a file that cannot be read back says so at once.
    private async Task SaveAsync(ConfigDraft draft)
    {
        var path = _host.ConfigPath;
        if (path is null)
        {
            if (PickSaveFile is not { } pick || await pick().ConfigureAwait(true) is not { } chosen)
            {
                return;
            }

            path = chosen;
        }

        if (_host.Save(draft.ToConfig(), path))
        {
            await FinishEditingAsync(path).ConfigureAwait(true);
        }
    }

    private async Task FinishEditingAsync(string? path)
    {
        Draft = null;
        SelectedCamera = null;
        SelectedMixer = null;
        SelectedInterfaceEntry = null;
        SelectedEntry = null;
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(CanUseFileMenu));

        if (path is not null)
        {
            await LoadAsync(path).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private Task AddCameraAsync() => AddAsync(EntryKind.Camera);

    [RelayCommand]
    private Task AddMixerAsync() => AddAsync(EntryKind.Mixer);

    [RelayCommand]
    private Task AddInterfaceAsync() => AddAsync(EntryKind.Interface);

    private async Task AddAsync(EntryKind kind)
    {
        if (Draft is not { } draft || AskToAdd is not { } ask)
        {
            return;
        }

        if (await ask(new NewEntry(kind, draft.NextInstance(kind), draft.Pads)).ConfigureAwait(true) is { } added)
        {
            draft.Insert(added);
            Select(added);
        }
    }

    [RelayCommand]
    private async Task DeleteEntryAsync(EntryDraft entry)
    {
        if (Draft is not { } draft || AskToDelete is not { } ask)
        {
            return;
        }

        if (await ask(entry, draft.ReferencesTo(entry)).ConfigureAwait(true))
        {
            draft.Remove(entry);
        }
    }

    /// Only pads that report a serial: one that does not cannot be named in a configuration, so
    /// offering it would be offering something the file cannot hold.
    private IReadOnlyList<string> ConnectedPads() =>
        [.. _host.Gamepads.Present.Select(pad => pad.Info.Serial).OfType<string>().Distinct(StringComparer.Ordinal)];

    private void Select(EntryDraft entry)
    {
        switch (entry.Kind)
        {
            case EntryKind.Camera:
                SelectedCamera = entry;
                break;
            case EntryKind.Mixer:
                SelectedMixer = entry;
                break;
            default:
                SelectedInterfaceEntry = entry;
                break;
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
        if (_host.ConfigPath is null)
        {
            ConfigPath = Localizer.Current.Text("config.none");
        }

        foreach (var language in Languages)
        {
            language.IsActive = Localizer.Current.Active == language.Language;
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (PickFile is { } pick && await pick().ConfigureAwait(true) is { } path)
        {
            await LoadAsync(path).ConfigureAwait(true);
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
            var view = new MixerViewModel(instance, mixer);
            view.PropertyChanged += OnChildChanged;
            Mixers.Add(view);
        }

        foreach (var camera in _host.Cameras)
        {
            var view = new CameraViewModel(camera);
            view.PropertyChanged += OnChildChanged;
            Cameras.Add(view);
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
                Owned(hmi, cameraViews, gamepad => gamepad.Cameras)));
        }

        // Anything no interface claims would otherwise not be drawn at all, and a camera missing
        // from the panel reads as one that failed to load rather than one nothing can reach.
        SelectedInterface = Interfaces.FirstOrDefault();

        UnassignedMixers = [.. Mixers.Where(view => Interfaces.All(entry => !entry.Mixers.Contains(view)))];
        UnassignedCameras = [.. Cameras.Where(view => Interfaces.All(entry => !entry.Cameras.Contains(view)))];

        OnPropertyChanged(nameof(UnassignedMixers));
        OnPropertyChanged(nameof(UnassignedCameras));
        OnPropertyChanged(nameof(HasUnassigned));
        OnChildChanged(this, new System.ComponentModel.PropertyChangedEventArgs(null));
    }

    private void OnChildChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(AllMixersConnected));
        OnPropertyChanged(nameof(AllCamerasConnected));
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
            mixer.PropertyChanged -= OnChildChanged;
            mixer.Dispose();
        }

        foreach (var camera in Cameras)
        {
            camera.PropertyChanged -= OnChildChanged;
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
