using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cgf.CameraControl.App.ViewModels;

/// The keyboard and mouse control surface, as the window drives it. Everything here goes to the
/// device and comes back out through the same interface a pad does, so the bindings, the special
/// functions and the tally all behave exactly as they do on a controller.
public sealed partial class ControlSurfaceViewModel(ControlSurfaceDevice device) : ViewModelBase
{
    public int Instance => device.Instance;

    /// Held rather than pressed: the modifier is what the next button means, so it stays down until
    /// it is clicked again. A keyboard holds Shift instead, and both write the same state.
    [ObservableProperty]
    public partial bool Alt { get; set; }

    [ObservableProperty]
    public partial bool AltLower { get; set; }

    /// Pan on X, tilt on Y.
    public void Move(double pan, double tilt) => device.Move(pan, tilt);

    /// Focus on X, zoom on Y.
    public void Lens(double focus, double zoom) => device.Lens(focus, zoom);

    public void Stop()
    {
        Move(0, 0);
        Lens(0, 0);
    }

    [RelayCommand]
    public void Select(ButtonDirection direction) => device.Select(direction);

    [RelayCommand]
    public void Run(ButtonDirection direction) => device.Run(direction);

    [RelayCommand]
    public void Transition(MixerTransition kind) => device.Transition(kind);

    partial void OnAltChanged(bool value) => device.SetModifiers(value, AltLower);

    partial void OnAltLowerChanged(bool value) => device.SetModifiers(Alt, value);
}
