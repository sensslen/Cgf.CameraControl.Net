using Avalonia.Controls;
using Avalonia.Input;
using Cgf.CameraControl.App.ViewModels;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.App.Views;

/// One interface, driven by hand. The pads are the two sticks and the keys below are the pad's
/// buttons, so an operator who knows the controller already knows this.
public partial class ControlSurfaceWindow : Window
{
    private readonly HashSet<Key> _held = [];

    public ControlSurfaceWindow()
    {
        InitializeComponent();

        // A window that goes away while a key or a pad is held would leave the camera running, and
        // closing it is the most likely way an operator stops using it. Losing activation is the
        // other way: no key up arrives for a key that was down when the window went to the back.
        Closed += (_, _) => Surface?.Stop();
        Deactivated += (_, _) =>
        {
            _held.Clear();
            Surface?.Stop();
        };
    }

    public ControlSurfaceWindow(InterfaceViewModel model)
        : this() => DataContext = model;

    private ControlSurfaceViewModel? Surface => (DataContext as InterfaceViewModel)?.Surface;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Surface is not { } surface)
        {
            return;
        }

        // A held key repeats, and every repeat would resend a position the camera already holds.
        if (!_held.Add(e.Key))
        {
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.Up:
                surface.Select(ButtonDirection.Up);
                break;
            case Key.Down:
                surface.Select(ButtonDirection.Down);
                break;
            case Key.Left:
                surface.Select(ButtonDirection.Left);
                break;
            case Key.Right:
                surface.Select(ButtonDirection.Right);
                break;
            case Key.D1 or Key.NumPad1:
                surface.Run(ButtonDirection.Down);
                break;
            case Key.D2 or Key.NumPad2:
                surface.Run(ButtonDirection.Right);
                break;
            case Key.D3 or Key.NumPad3:
                surface.Run(ButtonDirection.Left);
                break;
            case Key.D4 or Key.NumPad4:
                surface.Run(ButtonDirection.Up);
                break;
            case Key.Enter:
                surface.Transition(MixerTransition.Cut);
                break;
            case Key.Space:
                surface.Transition(MixerTransition.Auto);
                break;
            default:
                Apply(surface);
                break;
        }

        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _held.Remove(e.Key);
        if (Surface is { } surface)
        {
            Apply(surface);
        }
    }

    private void Apply(ControlSurfaceViewModel surface)
    {
        surface.Move(Axis(Key.D, Key.A), Axis(Key.W, Key.S));
        surface.Lens(Axis(Key.L, Key.J), Axis(Key.I, Key.K));
        surface.Alt = _held.Contains(Key.LeftShift) || _held.Contains(Key.RightShift);
        surface.AltLower = _held.Contains(Key.LeftCtrl) || _held.Contains(Key.RightCtrl);
    }

    /// A key is either fully down or not down at all, so an axis with both ends held is stopped.
    private double Axis(Key positive, Key negative) =>
        (_held.Contains(positive) ? 1 : 0) - (_held.Contains(negative) ? 1 : 0);
}
