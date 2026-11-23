using System.Collections.ObjectModel;
using System.Globalization;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.App.ViewModels;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Scenes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cgf.CameraControl.App.Scenes;

/// A scene to start from in the new scene wizard. Nothing is "start from scratch".
public sealed record SceneChoice(string Display, CameraScene? Scene);

/// What the wizard decided. Null from the window means it was left without storing anything.
public sealed record SceneWizardOutcome(CameraScene? Saved, string? Replaced, bool Deleted);

/// Two steps: frame the cameras, then choose what each one stores. Nothing is written until Save,
/// and leaving at any point stores nothing.
public sealed partial class SceneWizardViewModel : ViewModelBase, IDisposable
{
    private readonly IReadOnlyList<CameraScene> _scenes;
    private readonly CancellationTokenSource _closing = new();
    private readonly TaskCompletionSource<SceneWizardOutcome?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public SceneWizardViewModel(
        ILocalizer strings,
        bool editing,
        IReadOnlyList<CameraScene> scenes,
        IEnumerable<KeyValuePair<int, ICameraConnection>> cameras)
    {
        Strings = strings;
        IsEditing = editing;
        _scenes = scenes;
        Cameras = [.. cameras.OrderBy(camera => camera.Key)
            .Select(camera => new SceneCameraViewModel(strings, camera.Key, camera.Value))];
        foreach (var camera in Cameras)
        {
            camera.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanSave));
        }

        BaseChoices = [new SceneChoice(Strings.Text("scene.startEmpty"), null),
            .. scenes.Select(scene => new SceneChoice(scene.Name, scene))];
        if (editing)
        {
            SelectedScene = scenes.FirstOrDefault();
        }
        else
        {
            SelectedBase = BaseChoices[0];
        }

        SelectedCamera = Cameras.FirstOrDefault(camera => camera.CanFrame || camera.IsChecking);
    }

    public ILocalizer Strings { get; }

    public bool IsEditing { get; }

    /// Asked before the edited scene is deleted. Without it, Delete does nothing.
    public Func<string, Task<bool>>? ConfirmDelete { get; set; }

    /// What the wizard decided, once it is left: an outcome on Save or Delete, null on Cancel.
    public Task<SceneWizardOutcome?> Completion => _completion.Task;

    public bool IsFinished => _completion.Task.IsCompleted;

    public bool IsNew => !IsEditing;

    public string Title => Strings.Text(IsEditing ? "scene.editTitle" : "scene.newTitle");

    public IReadOnlyList<CameraScene> Scenes => _scenes;

    public IReadOnlyList<SceneChoice> BaseChoices { get; }

    public ObservableCollection<SceneCameraViewModel> Cameras { get; }

    /// The scene being edited.
    [ObservableProperty]
    public partial CameraScene? SelectedScene { get; set; }

    /// The scene a new one starts from.
    [ObservableProperty]
    public partial SceneChoice? SelectedBase { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NameError), nameof(CanNext), nameof(CanSave))]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedCamera))]
    public partial SceneCameraViewModel? SelectedCamera { get; set; }

    public bool HasSelectedCamera => SelectedCamera is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFrameStep), nameof(CanDelete))]
    public partial bool IsIncludeStep { get; private set; }

    public bool IsFrameStep => !IsIncludeStep;

    public bool CanDelete => IsEditing && IsFrameStep;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial bool IsSaving { get; private set; }

    /// The write cannot be called back, so the wizard cannot be left while it runs.
    [ObservableProperty]
    public partial bool IsStoring { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSaveError))]
    public partial string? SaveError { get; set; }

    public bool HasSaveError => SaveError is not null;

    public string? NameError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return Strings.Text("scene.nameEmpty");
            }

            return _scenes.Any(scene => CameraScene.SameName(scene.Name, Name)
                && !(IsEditing && SelectedScene is { } edited && ReferenceEquals(scene, edited)))
                ? string.Format(CultureInfo.CurrentUICulture, Strings.Text("scene.nameTaken"), Name.Trim())
                : null;
        }
    }

    public bool CanNext => NameError is null && (IsNew || SelectedScene is not null);

    public bool CanSave => CanNext && !IsSaving
        && Cameras.All(camera => !camera.IsChecking)
        && Cameras.Any(camera => camera.WillStore);

    /// The probe runs once per opening and in parallel, each camera on its own queue. Choices that
    /// depend on it stay unavailable until its camera has answered.
    public Task ProbeAsync() => Task.WhenAll(Cameras.Select(async camera =>
    {
        try
        {
            await camera.ProbeAsync(_closing.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The wizard closed first.
        }

        if (SelectedCamera is not { CanFrame: true } && camera.CanFrame)
        {
            SelectedCamera = camera;
        }
    }));

    partial void OnSelectedSceneChanged(CameraScene? value)
    {
        if (value is not null)
        {
            Name = value.Name;
        }

        Use(value);
    }

    partial void OnSelectedBaseChanged(SceneChoice? value) => Use(value?.Scene);

    /// The pads drive whichever camera is chosen, so the one they leave is stopped.
    partial void OnSelectedCameraChanged(SceneCameraViewModel? oldValue, SceneCameraViewModel? newValue)
    {
        if (oldValue is { CanFrame: true })
        {
            oldValue.Stop();
        }
    }

    [RelayCommand]
    private void Next()
    {
        if (!CanNext)
        {
            return;
        }

        foreach (var camera in Cameras)
        {
            camera.IncludeMoved();
        }

        IsIncludeStep = true;
        OnPropertyChanged(nameof(CanSave));
    }

    [RelayCommand]
    private void Back()
    {
        SaveError = null;
        IsIncludeStep = false;
    }

    /// Leaves without storing anything.
    [RelayCommand]
    public void Cancel()
    {
        if (!IsStoring && _completion.TrySetResult(null))
        {
            _closing.Cancel();
        }
    }

    /// Writes what the wizard decided and reports whether it landed. Without it, the wizard simply
    /// finishes with its outcome.
    public Func<SceneWizardOutcome, Task<bool>>? Commit { get; set; }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (await BuildAsync().ConfigureAwait(true) is { } outcome)
        {
            await FinishAsync(outcome).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!IsEditing || SelectedScene is not { } scene || ConfirmDelete is not { } confirm)
        {
            return;
        }

        if (await confirm(scene.Name).ConfigureAwait(true))
        {
            await FinishAsync(new SceneWizardOutcome(null, scene.Name, Deleted: true)).ConfigureAwait(true);
        }
    }

    /// The wizard stays open with its choices when the write fails, so the operator can try again.
    private async Task FinishAsync(SceneWizardOutcome outcome)
    {
        // Left while the cameras were answering: nothing is stored.
        if (IsFinished)
        {
            return;
        }

        IsSaving = true;
        IsStoring = true;
        try
        {
            if (Commit is { } commit && !await commit(outcome).ConfigureAwait(true))
            {
                SaveError = Strings.Text("scene.saveFailed");
                return;
            }
        }
        finally
        {
            IsStoring = false;
            IsSaving = false;
        }

        _completion.TrySetResult(outcome);
    }

    /// The scene as stored, or null with SaveError set. Cameras the scene stores that are not
    /// configured any more are kept as they were, because nothing here could have changed them.
    /// The name and the scene it replaces are taken before the cameras are read, so nothing chosen
    /// while they answer can mix two scenes.
    public async Task<SceneWizardOutcome?> BuildAsync()
    {
        if (!CanSave)
        {
            return null;
        }

        var name = Name.Trim();
        var seed = IsEditing ? SelectedScene : SelectedBase?.Scene;
        var replaced = IsEditing ? SelectedScene?.Name : null;
        var closing = _closing.Token;
        IsSaving = true;
        SaveError = null;
        try
        {
            var stored = new Dictionary<int, ScenePosition>();
            foreach (var camera in Cameras)
            {
                if (await camera.ComposeAsync(closing).ConfigureAwait(true) is { IsEmpty: false } position)
                {
                    stored[camera.Instance] = ScenePosition.From(position);
                }
            }

            // A camera that is not configured, or did not answer the probe, could not be framed or
            // chosen here, so whatever the scene held for it stays.
            foreach (var (instance, position) in seed?.Cameras ?? new Dictionary<int, ScenePosition>())
            {
                if (Cameras.FirstOrDefault(camera => camera.Instance == instance) is not { CanFrame: true })
                {
                    stored[instance] = position;
                }
            }

            return new SceneWizardOutcome(new CameraScene(name, stored), replaced, Deleted: false);
        }
        catch (OperationCanceledException) when (closing.IsCancellationRequested)
        {
            // The wizard was left while the cameras were being read.
            return null;
        }
        catch (InvalidOperationException ex)
        {
            SaveError = ex.Message;
            return null;
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// Every camera the wizard could have driven is stopped, so none keeps moving on a pad that was
    /// let go of as the window closed.
    public void Dispose()
    {
        _completion.TrySetResult(null);
        _closing.Cancel();
        foreach (var camera in Cameras.Where(camera => camera.CanFrame))
        {
            camera.Stop();
        }

        _closing.Dispose();
    }

    private void Use(CameraScene? scene)
    {
        foreach (var camera in Cameras)
        {
            camera.UseStored(scene?.Cameras.GetValueOrDefault(camera.Instance)?.Position);
        }

        OnPropertyChanged(nameof(NameError));
        OnPropertyChanged(nameof(CanNext));
        OnPropertyChanged(nameof(CanSave));
    }
}
