using System.Reactive.Disposables;
using System.Reactive.Linq;
using Cgf.CameraControl.App.Hosting;
using Cgf.CameraControl.Core.CameraConnection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cgf.CameraControl.App.ViewModels;

public sealed partial class CameraViewModel : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable _subscriptions = [];
    private readonly ICameraColorControl? _colorControl;
    private CameraColorState? _latestColor;
    private bool _updatingColor;
    private bool _disposed;

    public CameraViewModel(ObservedCamera camera)
    {
        ConnectionString = camera.ConnectionString;
        _colorControl = camera.ColorControl;

        if (_colorControl is { } color)
        {
            _subscriptions.Add(color.WhenColorChanged
                .Do(state => Volatile.Write(ref _latestColor, state))
                .Bind(UpdateColor));
        }

        _subscriptions.Add(camera.WhenConnectedChanged.Bind(connected =>
        {
            if (_disposed)
            {
                return;
            }

            IsConnected = connected;
            if (connected)
            {
                _colorControl?.RefreshColor();
            }
        }));
        _subscriptions.Add(camera.WhenTallyChanged.Bind(tally => Tally = tally));
        _subscriptions.Add(camera.WhenMovementChanged.Bind(movement => Movement = movement));
    }

    public string ConnectionString { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCorrectColor))]
    [NotifyPropertyChangedFor(nameof(CanAdjustGains))]
    [NotifyCanExecuteChangedFor(nameof(OnePushWhiteBalanceCommand))]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    public partial bool SupportsColorCorrection { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCorrectColor))]
    [NotifyPropertyChangedFor(nameof(CanAdjustGains))]
    [NotifyCanExecuteChangedFor(nameof(OnePushWhiteBalanceCommand))]
    public partial bool HasColorValues { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdjustGains))]
    public partial bool IsManualWhiteBalance { get; set; }

    [ObservableProperty]
    public partial int RedGain { get; set; }

    [ObservableProperty]
    public partial int BlueGain { get; set; }

    public bool CanCorrectColor => IsConnected && HasColorValues;

    public bool CanAdjustGains => CanCorrectColor && IsManualWhiteBalance;

    [RelayCommand(CanExecute = nameof(CanCorrectColor))]
    private void OnePushWhiteBalance() => ApplyColorChange(color => color.OnePushWhiteBalance());

    public void RefreshColor() => _colorControl?.RefreshColor();

    partial void OnIsConnectedChanged(bool value)
    {
        if (!value)
        {
            SupportsColorCorrection = false;
        }
    }

    partial void OnIsManualWhiteBalanceChanged(bool value)
    {
        if (!_updatingColor && CanCorrectColor)
        {
            ApplyColorChange(color => color.SetManualWhiteBalance(value));
        }
    }

    partial void OnRedGainChanged(int value)
    {
        if (!_updatingColor && CanAdjustGains)
        {
            ApplyColorChange(color => color.SetRedGain(value), requiresManual: true);
        }
    }

    partial void OnBlueGainChanged(int value)
    {
        if (!_updatingColor && CanAdjustGains)
        {
            ApplyColorChange(color => color.SetBlueGain(value), requiresManual: true);
        }
    }

    private void ApplyColorChange(Action<ICameraColorControl> apply, bool requiresManual = false)
    {
        if (_colorControl is not { } color || _disposed)
        {
            return;
        }

        try
        {
            apply(color);
        }
        catch (InvalidOperationException) when (Volatile.Read(ref _latestColor) is not { } state
            || requiresManual && !state.IsManual)
        {
            UpdateColor(Volatile.Read(ref _latestColor));
            color.RefreshColor();
        }
    }

    private void UpdateColor(CameraColorState? state)
    {
        if (_disposed)
        {
            return;
        }

        _updatingColor = true;
        try
        {
            HasColorValues = state is not null;
            if (state is not null)
            {
                SupportsColorCorrection = true;
                IsManualWhiteBalance = state.IsManual;
                RedGain = state.RedGain;
                BlueGain = state.BlueGain;
            }
        }
        finally
        {
            _updatingColor = false;
        }
    }

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

    public void Dispose()
    {
        _disposed = true;
        _subscriptions.Dispose();
    }

    private static string Percent(double value) => $"{value * 100:+0;-0;0}%";
}
