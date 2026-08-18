using System.Reactive.Disposables;
using Cgf.CameraControl.Core.Hmi;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cgf.CameraControl.App.ViewModels;

public sealed partial class InterfaceViewModel : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable _subscriptions = [];

    public InterfaceViewModel(int instance, IHmi hmi)
    {
        Instance = instance;
        Description = hmi.Description;

        // A gamepad describes itself by the pad it is bound to, and it binds at the moment it
        // reports itself connected.
        _subscriptions.Add(hmi.WhenConnectedChanged.Bind(connected =>
        {
            IsConnected = connected;
            Description = hmi.Description;
        }));
    }

    public int Instance { get; }

    [ObservableProperty]
    public partial string Description { get; set; }

    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    public void Dispose() => _subscriptions.Dispose();
}
