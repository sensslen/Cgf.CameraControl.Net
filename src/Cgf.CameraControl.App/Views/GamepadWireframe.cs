using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.App.Views;

/// The pad as the application sees it. Only the controls an interface reads are drawn: a shape that
/// can never light up is a bug report waiting to happen, so there is no guide button here, no start
/// or back, and no stick click.
///
/// Every coordinate is a fraction of a 1 by 1/Aspect box and is scaled at paint time, so the drawing
/// keeps its proportions whatever the panel around it does.
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

    /// A pad is about half again as wide as it is tall once the grips are counted.
    private const double Aspect = 1.55;

    private static readonly Point DPadCentre = new(0.255, 0.34);
    private static readonly Point FaceCentre = new(0.745, 0.34);
    private static readonly Point LeftStickCentre = new(0.375, 0.52);
    private static readonly Point RightStickCentre = new(0.625, 0.52);

    static GamepadWireframe() =>
        AffectsRender<GamepadWireframe>(StateProperty, IsLiveProperty, FaceLabelsProperty, DPadLabelsProperty);

    private enum Side
    {
        Left,
        Right,
        Above,
        Below,
    }

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

        using var faded = context.PushOpacity(IsLive ? 1 : 0.4);
        Body(context, origin, scale, pen);
        Shoulders(context, origin, scale, pen, state);
        DPad(context, origin, scale, pen, state);
        Faces(context, origin, scale, pen, state);
        Stick(context, origin, scale, pen, LeftStickCentre, state.LeftStick);
        Stick(context, origin, scale, pen, RightStickCentre, state.RightStick);
    }

    private static Color Outline => Color.FromArgb(0xC0, 0x8A, 0x8A, 0x8A);

    private static Color Ink => Color.FromArgb(0xD0, 0x60, 0x60, 0x60);

    private static Color Lit => Color.FromRgb(0x4A, 0x9E, 0xE0);

    private static IBrush Brush(Color color) => new SolidColorBrush(color);

    private static Point At(Point origin, double scale, double x, double y) =>
        new(origin.X + (x * scale), origin.Y + (y * scale / Aspect));

    /// A control the operator is holding is filled; one they are not is an outline.
    private IBrush? Fill(GamepadState state, GamepadButtons button) =>
        IsLive && state.IsPressed(button) ? Brush(Lit) : null;

    /// The silhouette: a body that swells at the shoulders, dips between them, and drops into two
    /// grips. Drawn as one closed path so the grips are part of the shape rather than stuck on.
    private static void Body(DrawingContext context, Point origin, double scale, Pen pen)
    {
        var shape = new StreamGeometry();
        using (var path = shape.Open())
        {
            Point P(double x, double y) => At(origin, scale, x, y);

            path.BeginFigure(P(0.18, 0.10), isFilled: false);
            path.CubicBezierTo(P(0.30, 0.05), P(0.42, 0.14), P(0.50, 0.15));
            path.CubicBezierTo(P(0.58, 0.14), P(0.70, 0.05), P(0.82, 0.10));
            path.CubicBezierTo(P(0.94, 0.15), P(1.00, 0.30), P(0.98, 0.48));
            path.CubicBezierTo(P(0.96, 0.74), P(0.89, 0.94), P(0.78, 0.93));
            path.CubicBezierTo(P(0.69, 0.92), P(0.64, 0.78), P(0.61, 0.64));
            path.CubicBezierTo(P(0.58, 0.55), P(0.42, 0.55), P(0.39, 0.64));
            path.CubicBezierTo(P(0.36, 0.78), P(0.31, 0.92), P(0.22, 0.93));
            path.CubicBezierTo(P(0.11, 0.94), P(0.04, 0.74), P(0.02, 0.48));
            path.CubicBezierTo(P(0.00, 0.30), P(0.06, 0.15), P(0.18, 0.10));
            path.EndFigure(isClosed: true);
        }

        context.DrawGeometry(null, pen, shape);
    }

    /// Two on each side. The left pair holds the modifiers and the right pair works the transition,
    /// which is what the labels say so that nobody has to remember it.
    private void Shoulders(DrawingContext context, Point origin, double scale, Pen pen, GamepadState state)
    {
        Bar(0.09, 0.005, 0.29, 0.045, state.LeftTrigger, null, "alt lower");
        Bar(0.11, 0.055, 0.31, 0.095, 0, GamepadButtons.LeftShoulder, "alt");
        Bar(0.71, 0.005, 0.91, 0.045, state.RightTrigger, null, "Auto");
        Bar(0.69, 0.055, 0.89, 0.095, 0, GamepadButtons.RightShoulder, "Cut");

        void Bar(double x1, double y1, double x2, double y2, double travel, GamepadButtons? button, string label)
        {
            var outline = new Rect(At(origin, scale, x1, y1), At(origin, scale, x2, y2));
            context.DrawRectangle(
                button is { } held ? Fill(state, held) : null,
                pen,
                new RoundedRect(outline, scale * 0.014));

            // A shoulder is a press and a trigger is travel, so one fills and the other fills up.
            if (IsLive && travel > 0)
            {
                var width = outline.Width * Math.Clamp(travel, 0, 1);
                context.FillRectangle(Brush(Lit), new Rect(outline.X, outline.Y, width, outline.Height));
            }

            Centred(context, label, outline);
        }
    }

    private void DPad(DrawingContext context, Point origin, double scale, Pen pen, GamepadState state)
    {
        var arm = scale * 0.030;
        var middle = At(origin, scale, DPadCentre.X, DPadCentre.Y);
        context.DrawEllipse(null, pen, middle, arm * 2.5, arm * 2.5);

        Arm(0, -1, GamepadButtons.DPadUp, 0, Side.Above);
        Arm(0, 1, GamepadButtons.DPadDown, 1, Side.Below);
        Arm(-1, 0, GamepadButtons.DPadLeft, 2, Side.Left);
        Arm(1, 0, GamepadButtons.DPadRight, 3, Side.Right);

        void Arm(double dx, double dy, GamepadButtons button, int label, Side at)
        {
            var tip = new Point(middle.X + (dx * arm), middle.Y + (dy * arm));
            var half = new Size(dx == 0 ? arm * 0.5 : arm * 0.9, dy == 0 ? arm * 0.5 : arm * 0.9);
            var pad = new Rect(
                new Point(tip.X - half.Width, tip.Y - half.Height),
                new Point(tip.X + half.Width, tip.Y + half.Height));
            context.DrawRectangle(Fill(state, button), pen, new RoundedRect(pad, arm * 0.2));
            Label(context, DPadLabels, label, pad, at);
        }
    }

    private void Faces(DrawingContext context, Point origin, double scale, Pen pen, GamepadState state)
    {
        var reach = scale * 0.058;
        var radius = scale * 0.030;
        var middle = At(origin, scale, FaceCentre.X, FaceCentre.Y);
        context.DrawEllipse(null, pen, middle, reach + radius + (scale * 0.008), reach + radius + (scale * 0.008));

        Face(0, -1, GamepadButtons.FaceUp, 0, Side.Above);
        Face(0, 1, GamepadButtons.FaceDown, 1, Side.Below);
        Face(-1, 0, GamepadButtons.FaceLeft, 2, Side.Left);
        Face(1, 0, GamepadButtons.FaceRight, 3, Side.Right);

        void Face(double dx, double dy, GamepadButtons button, int label, Side at)
        {
            var centre = new Point(middle.X + (dx * reach), middle.Y + (dy * reach));
            context.DrawEllipse(Fill(state, button), pen, centre, radius, radius);
            Label(
                context,
                FaceLabels,
                label,
                new Rect(centre.X - radius, centre.Y - radius, radius * 2, radius * 2),
                at);
        }
    }

    /// The knob sits where the stick is pushed, inside the ring that bounds its travel.
    private void Stick(DrawingContext context, Point origin, double scale, Pen pen, Point centre, StickPosition at)
    {
        var middle = At(origin, scale, centre.X, centre.Y);
        var ring = scale * 0.068;
        context.DrawEllipse(null, pen, middle, ring, ring);

        var knob = new Point(middle.X + (at.X * ring * 0.55), middle.Y - (at.Y * ring * 0.55));
        var moved = IsLive && (Math.Abs(at.X) > 0.001 || Math.Abs(at.Y) > 0.001);
        context.DrawEllipse(moved ? Brush(Lit) : null, pen, knob, ring * 0.55, ring * 0.55);
    }

    /// An unbound control carries no label, which is what makes the drawing say the same thing the
    /// configuration file does.
    private static void Label(
        DrawingContext context,
        IReadOnlyList<string?> labels,
        int index,
        Rect control,
        Side at)
    {
        if (index >= labels.Count || labels[index] is not { Length: > 0 } text)
        {
            return;
        }

        var formatted = Text(text);
        var gap = 8.0;
        var origin = at switch
        {
            Side.Left => new Point(control.X - gap - formatted.Width, control.Center.Y - (formatted.Height / 2)),
            Side.Right => new Point(control.Right + gap, control.Center.Y - (formatted.Height / 2)),
            Side.Above => new Point(control.Center.X - (formatted.Width / 2), control.Y - gap - formatted.Height),
            _ => new Point(control.Center.X - (formatted.Width / 2), control.Bottom + gap),
        };

        context.DrawText(formatted, origin);
    }

    private static void Centred(DrawingContext context, string text, Rect control)
    {
        var formatted = Text(text);
        context.DrawText(
            formatted,
            new Point(
                control.Center.X - (formatted.Width / 2),
                control.Center.Y - (formatted.Height / 2)));
    }

    private static FormattedText Text(string text) => new(
        text,
        CultureInfo.CurrentUICulture,
        FlowDirection.LeftToRight,
        Typeface.Default,
        10.5,
        Brush(Ink));
}
