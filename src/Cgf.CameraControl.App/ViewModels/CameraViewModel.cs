using System.Reactive.Disposables;
using Cgf.CameraControl.App.Hosting;
using Cgf.CameraControl.Core.CameraConnection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cgf.CameraControl.App.ViewModels;

public sealed partial class CameraViewModel : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable _subscriptions = [];

    public CameraViewModel(ObservedCamera camera)
    {
        ConnectionString = camera.ConnectionString;

        _subscriptions.Add(camera.WhenConnectedChanged.Bind(connected => IsConnected = connected));
        _subscriptions.Add(camera.WhenTallyChanged.Bind(tally => Tally = tally));
        _subscriptions.Add(camera.WhenMovementChanged.Bind(movement => Movement = movement));
    }

    public string ConnectionString { get; }

    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnProgram))]
    [NotifyPropertyChangedFor(nameof(IsOnPreview))]
    public partial TallyState Tally { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MovementText))]
    public partial CameraMovement Movement { get; set; }

    public bool IsOnProgram => Tally == TallyState.Program;

    public bool IsOnPreview => Tally == TallyState.Preview;

    public string MovementText =>
        $"P {Percent(Movement.Pan)}  T {Percent(Movement.Tilt)}  Z {Percent(Movement.Zoom)}  F {Percent(Movement.Focus)}";

    public void Dispose() => _subscriptions.Dispose();

    private static string Percent(double value) => $"{value * 100:+0;-0;0}%";
}
