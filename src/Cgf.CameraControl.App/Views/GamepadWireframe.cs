using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.App.Views;

/// The pad as the application sees it. Only the controls an interface reads are drawn: a shape that
/// can never light up is a bug report waiting to happen, so there is no guide button here and no
/// stick click.
///
/// Every coordinate is a fraction of the control and is scaled at paint time, because the panel this
/// sits in is resized by the operator dragging the log splitter.
public sealed class GamepadWireframe : Control
{
    public static readonly StyledProperty<GamepadState> StateProperty =
        AvaloniaProperty.Register<GamepadWireframe, GamepadState>(nameof(State));

    /// A pad that is not there is drawn rather than hidden, because the shape beside a dead lamp
    /// says "unplugged" where an empty panel says nothing at all.
    public static readonly StyledProperty<bool> IsLiveProperty =
        AvaloniaProperty.Register<GamepadWireframe, bool>(nameof(IsLive), defaultValue: true);

    /// What pressing each control would cause, in the order up, down, left, right. Face labels change
    /// with the held modifier, so they arrive as text rather than being derived from a binding table
    /// this control would otherwise have to understand.
    public static readonly StyledProperty<IReadOnlyList<string?>> FaceLabelsProperty =
        AvaloniaProperty.Register<GamepadWireframe, IReadOnlyList<string?>>(nameof(FaceLabels), []);

    public static readonly StyledProperty<IReadOnlyList<string?>> DPadLabelsProperty =
        AvaloniaProperty.Register<GamepadWireframe, IReadOnlyList<string?>>(nameof(DPadLabels), []);

    private static readonly Point LeftStickCentre = new(0.20, 0.34);
    private static readonly Point DPadCentre = new(0.38, 0.68);
    private static readonly Point FaceCentre = new(0.78, 0.34);
    private static readonly Point RightStickCentre = new(0.62, 0.68);

    static GamepadWireframe() =>
        AffectsRender<GamepadWireframe>(StateProperty, IsLiveProperty, FaceLabelsProperty, DPadLabelsProperty);

    public GamepadState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public bool IsLive
    {
        get => GetValue(IsLiveProperty);
        set => SetValue(IsLiveProperty, value);
    }

    public IReadOnlyList<string?> FaceLabels
    {
        get => GetValue(FaceLabelsProperty);
        set => SetValue(FaceLabelsProperty, value);
    }

    public IReadOnlyList<string?> DPadLabels
    {
        get => GetValue(DPadLabelsProperty);
        set => SetValue(DPadLabelsProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var scale = Math.Min(size.Width, size.Height * Aspect);
        var origin = new Point((size.Width - scale) / 2, (size.Height - (scale / Aspect)) / 2);
        var pen = new Pen(Brush(Outline), 1.6);
        var state = IsLive ? State : default;

        using var faded = context.PushOpacity(IsLive ? 1 : 0.35);
        Body(context, origin, scale, pen);
        Shoulders(context, origin, scale, pen, state);
        Stick(context, origin, scale, pen, LeftStickCentre, state.LeftStick);
        Stick(context, origin, scale, pen, RightStickCentre, state.RightStick);
        DPad(context, origin, scale, pen, state);
        Faces(context, origin, scale, pen, state);
    }

    /// A pad is about twice as wide as it is tall, which is what keeps the drawing a pad rather than
    /// a square when the panel around it is not.
    private static double Aspect => 1.9;

    private static Color Outline => Color.FromArgb(0xB0, 0x9A, 0x9A, 0x9A);

    private static Color Lit => Color.FromRgb(0x4A, 0x9E, 0xE0);

    private static IBrush Brush(Color color) => new SolidColorBrush(color);

    /// A control the operator is holding is filled; one they are not is an outline.
    private IBrush? Fill(GamepadState state, GamepadButtons button) =>
        IsLive && state.IsPressed(button) ? Brush(Lit) : null;

    private static Point At(Point origin, double scale, Point fraction) =>
        new(origin.X + (fraction.X * scale), origin.Y + (fraction.Y * scale / Aspect));

    private static void Body(DrawingContext context, Point origin, double scale, Pen pen)
    {
        var body = new Rect(At(origin, scale, new Point(0.02, 0.14)), At(origin, scale, new Point(0.98, 0.98)));
        context.DrawRectangle(null, pen, new RoundedRect(body, scale * 0.12));
    }

    /// A shoulder is a press and a trigger is travel, so one is filled and the other fills up.
    private void Shoulders(DrawingContext context, Point origin, double scale, Pen pen, GamepadState state)
    {
        Shoulder(new Point(0.10, 0.02), state.LeftTrigger, GamepadButtons.LeftShoulder);
        Shoulder(new Point(0.72, 0.02), state.RightTrigger, GamepadButtons.RightShoulder);

        void Shoulder(Point corner, double travel, GamepadButtons button)
        {
            var outline = new Rect(
                At(origin, scale, corner),
                At(origin, scale, new Point(corner.X + 0.18, corner.Y + 0.12)));
            context.DrawRectangle(Fill(state, button), pen, new RoundedRect(outline, scale * 0.02));

            if (IsLive && travel > 0)
            {
                var depth = outline.Height * Math.Clamp(travel, 0, 1);
                context.FillRectangle(Brush(Lit), new Rect(outline.X, outline.Bottom - depth, outline.Width, depth));
            }
        }
    }

    /// The knob sits where the stick is pushed, inside the ring that bounds its travel.
    private void Stick(DrawingContext context, Point origin, double scale, Pen pen, Point centre, StickPosition at)
    {
        var middle = At(origin, scale, centre);
        var ring = scale * 0.085;
        context.DrawEllipse(null, pen, middle, ring, ring);

        var knob = new Point(middle.X + (at.X * ring * 0.6), middle.Y - (at.Y * ring * 0.6));
        var moved = IsLive && (Math.Abs(at.X) > 0.001 || Math.Abs(at.Y) > 0.001);
        context.DrawEllipse(moved ? Brush(Lit) : Brush(Outline), null, knob, ring * 0.34, ring * 0.34);
    }

    private void DPad(DrawingContext context, Point origin, double scale, Pen pen, GamepadState state)
    {
        var arm = scale * 0.038;
        Arm(new Point(0, -1), GamepadButtons.DPadUp, 0);
        Arm(new Point(0, 1), GamepadButtons.DPadDown, 1);
        Arm(new Point(-1, 0), GamepadButtons.DPadLeft, 2);
        Arm(new Point(1, 0), GamepadButtons.DPadRight, 3);

        void Arm(Point direction, GamepadButtons button, int label)
        {
            var middle = At(origin, scale, DPadCentre);
            var tip = new Point(middle.X + (direction.X * arm * 1.6), middle.Y + (direction.Y * arm * 1.6));
            var pad = new Rect(
                new Point(tip.X - (arm * 0.55), tip.Y - (arm * 0.55)),
                new Point(tip.X + (arm * 0.55), tip.Y + (arm * 0.55)));
            context.DrawRectangle(Fill(state, button), pen, new RoundedRect(pad, arm * 0.2));
            Label(context, DPadLabels, label, new Point(pad.Right + (arm * 0.4), pad.Center.Y));
        }
    }

    private void Faces(DrawingContext context, Point origin, double scale, Pen pen, GamepadState state)
    {
        var reach = scale * 0.062;
        var radius = scale * 0.032;
        Face(new Point(0, -1), GamepadButtons.FaceUp, 0);
        Face(new Point(0, 1), GamepadButtons.FaceDown, 1);
        Face(new Point(-1, 0), GamepadButtons.FaceLeft, 2);
        Face(new Point(1, 0), GamepadButtons.FaceRight, 3);

        void Face(Point direction, GamepadButtons button, int label)
        {
            var middle = At(origin, scale, FaceCentre);
            var at = new Point(middle.X + (direction.X * reach), middle.Y + (direction.Y * reach));
            context.DrawEllipse(Fill(state, button), pen, at, radius, radius);
            Label(context, FaceLabels, label, new Point(at.X + radius + (scale * 0.012), at.Y));
        }
    }

    /// An unbound control carries no label, which is what makes the drawing say the same thing the
    /// configuration file does.
    private static void Label(DrawingContext context, IReadOnlyList<string?> labels, int index, Point at)
    {
        if (index >= labels.Count || labels[index] is not { Length: > 0 } text)
        {
            return;
        }

        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            11,
            Brush(Outline));

        context.DrawText(formatted, new Point(at.X, at.Y - (formatted.Height / 2)));
    }
}
