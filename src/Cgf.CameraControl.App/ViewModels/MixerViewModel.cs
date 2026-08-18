using System.Reactive.Disposables;
using Cgf.CameraControl.Core.VideoMixer;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cgf.CameraControl.App.ViewModels;

public sealed partial class MixerViewModel : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable _subscriptions = [];

    public MixerViewModel(int instance, IVideoMixer mixer)
    {
        Instance = instance;
        ConnectionString = mixer.ConnectionString;

        _subscriptions.Add(mixer.WhenConnectedChanged.Bind(connected => IsConnected = connected));
        _subscriptions.Add(mixer.WhenPreviewChanged.Bind(change =>
        {
            Preview = change.Preview;
            IsPreviewOnAir = change.OnAir;
        }));
        _subscriptions.Add(mixer.WhenProgramChanged.Bind(program => Program = program));
    }

    public int Instance { get; }

    public string ConnectionString { get; }

    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    public partial int Preview { get; set; } = -1;

    [ObservableProperty]
    public partial int Program { get; set; } = -1;

    [ObservableProperty]
    public partial bool IsPreviewOnAir { get; set; }

    public void Dispose() => _subscriptions.Dispose();
}
