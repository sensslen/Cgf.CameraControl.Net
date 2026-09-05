using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace Cgf.CameraControl.App.Views;

/// A mouse stand-in for one gamepad stick: press anywhere inside it and the offset from the centre
/// is the speed on each axis, release and it springs back to nothing.
///
/// Springing back is the whole point. A camera driven by a control that stays where it was left
/// keeps moving after the operator has stopped paying attention to it, which is how a shot ends up
/// pointing at the ceiling.
public sealed class StickPad : Border
{
    private const double KnobSize = 28;

    private readonly Canvas _surface = new();
    private readonly Ellipse _knob = new()
    {
        Width = KnobSize,
        Height = KnobSize,
        Fill = Brushes.SteelBlue,
    };

    private double _x;
    private double _y;

    public StickPad()
    {
        _surface.Children.Add(_knob);
        Child = _surface;

        // A border with no background is not hit tested, so there would be nothing to press on.
        Background ??= Brushes.Transparent;
        Focusable = true;
    }

    /// The position of the stick, each axis in [-1 .. 1], with up and right positive.
    public event Action<double, double>? Moved;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Pointer.Capture(this);
        Track(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (ReferenceEquals(e.Pointer.Captured, this))
        {
            Track(e.GetPosition(this));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);
        Release();
    }

    /// Losing the pointer, to another window or to the window closing, has to stop the camera for
    /// the same reason releasing does.
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        Release();
    }

    /// The knob is placed in pixels, so it has to be placed again whenever the pad is resized.
    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        Place();
        return arranged;
    }

    private void Release()
    {
        _x = 0;
        _y = 0;
        Place();
        Moved?.Invoke(0, 0);
    }

    private void Track(Point pointer)
    {
        var half = new Point(_surface.Bounds.Width / 2, _surface.Bounds.Height / 2);
        if (half.X <= 0 || half.Y <= 0)
        {
            return;
        }

        _x = Math.Clamp((pointer.X - half.X) / half.X, -1, 1);
        _y = Math.Clamp((half.Y - pointer.Y) / half.Y, -1, 1);

        Place();
        Moved?.Invoke(_x, _y);
    }

    private void Place()
    {
        var half = new Point(_surface.Bounds.Width / 2, _surface.Bounds.Height / 2);
        Canvas.SetLeft(_knob, half.X + (_x * half.X) - (KnobSize / 2));
        Canvas.SetTop(_knob, half.Y - (_y * half.Y) - (KnobSize / 2));
    }
}
