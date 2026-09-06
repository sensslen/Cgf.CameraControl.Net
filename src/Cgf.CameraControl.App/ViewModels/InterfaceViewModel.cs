using System.Reactive.Disposables;
using Cgf.CameraControl.Core.Hmi;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cgf.CameraControl.App.ViewModels;

/// An interface and what it drives. The panel is grouped this way rather than by kind because a desk
/// is read that way: this operator, this mixer, these cameras.
public sealed partial class InterfaceViewModel : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable _subscriptions = [];

    public InterfaceViewModel(
        int instance,
        IHmi hmi,
        ControlSurfaceViewModel? surface,
        IReadOnlyList<MixerViewModel> mixers,
        IReadOnlyList<CameraViewModel> cameras)
    {
        Instance = instance;
        Description = hmi.Description;
        Surface = surface;
        Mixers = mixers;
        Cameras = cameras;

        // A gamepad describes itself by the pad it is bound to, and it binds at the moment it
        // reports itself connected.
        _subscriptions.Add(hmi.WhenConnectedChanged.Bind(connected =>
        {
            IsConnected = connected;
            Description = hmi.Description;
        }));
    }

    public int Instance { get; }

    /// Every interface has one: a pad is driven from the window as well as from the desk.
    public ControlSurfaceViewModel? Surface { get; }

    public IReadOnlyList<MixerViewModel> Mixers { get; }

    public IReadOnlyList<CameraViewModel> Cameras { get; }

    [ObservableProperty]
    public partial string Description { get; set; }

    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    public void Dispose() => _subscriptions.Dispose();
}
