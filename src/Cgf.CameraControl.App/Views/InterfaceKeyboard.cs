using Avalonia.Input;
using Cgf.CameraControl.App.ViewModels;

namespace Cgf.CameraControl.App.Views;

/// Turns what the window sees on the keyboard into what the selected interface asked for. Every key
/// here came out of the configuration file, so this knows which keys are held and nothing else about
/// what they mean.
public sealed class InterfaceKeyboard
{
    private readonly HashSet<Key> _held = [];

    private InterfaceViewModel? _target;

    /// Switching interfaces mid-press would leave the one being left running, in the same way that
    /// closing a window on a held key used to.
    public InterfaceViewModel? Target
    {
        get => _target;
        set
        {
            if (ReferenceEquals(_target, value))
            {
                return;
            }

            Release();
            _target = value;
        }
    }

    /// True when the key belonged to this interface, so the window can leave the rest to Avalonia.
    public bool Down(Key key)
    {
        if (Target is not { Surface: { } surface } target || !Binds(target, key))
        {
            return false;
        }

        // A held key repeats, and every repeat would resend a position the camera already holds.
        if (!_held.Add(key))
        {
            return true;
        }

        foreach (var button in target.Buttons.Where(button => button.Key == key))
        {
            button.IsHeld = true;
            if (button.Command.CanExecute(null))
            {
                button.Command.Execute(null);
            }
        }

        if (target.Keys.ConnectionChange.TryGetValue(key, out var direction))
        {
            surface.Select(direction);
        }

        Apply(target, surface);
        return true;
    }

    public bool Up(Key key)
    {
        if (Target is not { Surface: { } surface } target || !_held.Remove(key))
        {
            return false;
        }

        foreach (var button in target.Buttons.Where(button => button.Key == key))
        {
            button.IsHeld = false;
        }

        Apply(target, surface);
        return true;
    }

    /// Losing the window with a key down means no key up ever arrives, and a camera left running is
    /// how a shot ends up pointing at the ceiling.
    public void Release()
    {
        _held.Clear();
        if (Target is { Surface: { } surface } target)
        {
            foreach (var button in target.Buttons)
            {
                button.IsHeld = false;
            }

            Apply(target, surface);
        }
    }

    private static bool Binds(InterfaceViewModel target, Key key) =>
        target.Buttons.Any(button => button.Key == key)
        || target.Keys.ConnectionChange.ContainsKey(key)
        || Axes(target.Keys).Contains(key);

    private static IEnumerable<Key> Axes(InterfaceKeys keys) =>
        new[]
        {
            keys.PanLeft, keys.PanRight, keys.TiltUp, keys.TiltDown,
            keys.ZoomIn, keys.ZoomOut, keys.FocusFar, keys.FocusNear,
        }.OfType<Key>();

    private void Apply(InterfaceViewModel target, ControlSurfaceViewModel surface)
    {
        var keys = target.Keys;
        surface.Move(Axis(keys.PanRight, keys.PanLeft), Axis(keys.TiltUp, keys.TiltDown));
        surface.Lens(Axis(keys.FocusFar, keys.FocusNear), Axis(keys.ZoomIn, keys.ZoomOut));
    }

    /// A key is either fully down or not down at all, so an axis with both ends held is stopped.
    private double Axis(Key? positive, Key? negative) =>
        (positive is { } up && _held.Contains(up) ? 1 : 0) -
        (negative is { } down && _held.Contains(down) ? 1 : 0);
}
