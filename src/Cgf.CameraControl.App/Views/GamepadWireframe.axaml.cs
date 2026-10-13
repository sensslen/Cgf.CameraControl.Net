using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.App.ViewModels;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using Path = Avalonia.Controls.Shapes.Path;

namespace Cgf.CameraControl.App.Views;

/// The pad as the application sees it: a silhouette with the live state laid over it, and beside it
/// a legend that names each control under the symbol printed on it. Only the controls an interface
/// reads are listed, and the rest are drawn faded, so the drawing says what the configuration does.
public partial class GamepadWireframe : UserControl
{
    /// How far a knob travels at full deflection, in the drawing's own coordinates. It keeps the
    /// knob inside its ring, as a real stick stays in its well.
    private const double Travel = 9;

    private const double PadWidth = 360;
    private const double StickRing = 18;
    private const double KnobRadius = 8.5;
    private const double FaceRadius = 9;

    private static readonly Point DPadCentre = new(95, 92);
    private static readonly Point FaceCentre = new(265, 92);
    private static readonly Point LeftStickCentre = new(138, 124);
    private static readonly Point RightStickCentre = new(222, 124);

    /// The silhouette, symmetric about the middle: a flat top between the shoulders, grips that hang
    /// below the sticks, and a shallow arch between them.
    private const string Body =
        "M180,46 C150,46 122,40 100,40 C70,40 52,50 44,72 C34,100 22,150 26,182 C29,204 52,212 66,198 " +
        "C80,184 92,160 110,152 C130,144 150,148 180,148 C210,148 230,144 250,152 C268,160 280,184 294,198 " +
        "C308,212 331,204 334,182 C338,150 326,100 316,72 C308,50 290,40 260,40 C238,40 210,46 180,46 Z";

    /// The body's top left corner, the second segment of Body. The shoulders are bands stacked on it:
    /// the bumper's lower edge is a stretch of this curve and the trigger's lower edge a stretch of
    /// the bumper's upper edge, so neighbours share an edge exactly. The right side is the mirror.
    private static readonly Point[] Corner = [new(100, 40), new(70, 40), new(52, 50), new(44, 72)];

    private const double BumperDepth = 10;

    /// A trigger is the bigger, analog one, so it stands deeper and domes in the middle.
    private const double TriggerDepth = 13;
    private const double TriggerDome = 7;

    /// The four directions in the order the view model lists them: up, down, left, right.
    private static readonly Vector[] Directions = [new(0, -1), new(0, 1), new(-1, 0), new(1, 0)];

    /// The face buttons by position, in the same order, with the symbols a PlayStation pad prints.
    private static readonly string[] Symbols = ["triangle", "cross", "square", "circle"];

    private static readonly GamepadButtons[] DPadButtons =
        [GamepadButtons.DPadUp, GamepadButtons.DPadDown, GamepadButtons.DPadLeft, GamepadButtons.DPadRight];

    private static readonly GamepadButtons[] FaceButtons =
        [GamepadButtons.FaceUp, GamepadButtons.FaceDown, GamepadButtons.FaceLeft, GamepadButtons.FaceRight];

    private readonly Part[] _dPad = new Part[4];
    private readonly Part[] _face = new Part[4];
    private readonly Part _leftShoulder;
    private readonly Part _rightShoulder;
    private readonly Part _leftTrigger;
    private readonly Part _rightTrigger;
    private readonly Part _leftStick;
    private readonly Part _rightStick;
    private readonly Path _leftKnob = Knob();
    private readonly Path _rightKnob = Knob();

    private InterfaceViewModel? _model;

    public GamepadWireframe()
    {
        InitializeComponent();

        // Back to front: the triggers stand behind the bumpers, which the body's top edge covers.
        var bumper = Sub(Corner, 0, 0.8);
        var bumperTop = Offset(bumper, BumperDepth);
        var trigger = Sub(bumperTop, 0.1, 0.78);
        var triggerTop = Offset(trigger, TriggerDepth, TriggerDome);
        _leftTrigger = Add(Band(trigger, triggerTop, 7, "L2", mirrored: false), TabIcon("L2", tall: true));
        _rightTrigger = Add(Band(trigger, triggerTop, 7, "R2", mirrored: true), TabIcon("R2", tall: true));
        _leftShoulder = Add(Band(bumper, bumperTop, 4, "L1", mirrored: false).OnPad, TabIcon("L1", tall: false));
        _rightShoulder = Add(Band(bumper, bumperTop, 4, "R1", mirrored: true).OnPad, TabIcon("R1", tall: false));

        Pad.Children.Add(new Path { Data = Geometry.Parse(Body), Classes = { "body" } });
        Pad.Children.Add(new Path { Data = Square(DPadCentre, 7), Classes = { "control", "hub" } });

        for (var index = 0; index < 4; index++)
        {
            _dPad[index] = Add(ArmGlyph(index), KeyIcon(index));
            _face[index] = Add(OnPad(FaceGlyph(FaceCentre + (Directions[index] * 16), index)), FaceGlyph(new Point(11, 11), index));
        }

        _leftStick = Add(OnPad(StickGlyph(LeftStickCentre, StickRing, letter: null)), StickGlyph(new Point(11, 11), 10, "L"));
        _rightStick = Add(OnPad(StickGlyph(RightStickCentre, StickRing, letter: null)), StickGlyph(new Point(11, 11), 10, "R"));
        Pad.Children.Add(_leftKnob);
        Pad.Children.Add(_rightKnob);

        // The order a reader scans the legend in: what moves the camera, then what picks it, then
        // what the buttons do, then the held modifiers and the transitions. Each cluster is read
        // clockwise from the top.
        foreach (var part in new[] { _leftStick, _rightStick }.Concat(Clockwise(_dPad)).Concat(Clockwise(_face))
                     .Concat([_leftShoulder, _leftTrigger, _rightShoulder, _rightTrigger]))
        {
            Legend.Children.Add(part.Row);
        }
    }

    /// Camera-space pan/focus increase leftward, tilt increases downward, and zoom
    /// increases upward. Screen coordinates increase rightward and downward.
    public static Point StickOffset(StickPosition at, bool isLeftStick) =>
        new(-at.X * Travel, (isLeftStick ? at.Y : -at.Y) * Travel);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Localizer.Current.LanguageChanged += OnLanguageChanged;
        Follow();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Localizer.Current.LanguageChanged -= OnLanguageChanged;
        Watch(null);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (VisualRoot is not null)
        {
            Follow();
        }
    }

    private void Follow()
    {
        Watch(DataContext as InterfaceViewModel);
        Relabel();
        Draw();
    }

    /// One model at a time: a view that outlives the selection must stop redrawing for the
    /// interface it showed before.
    private void Watch(InterfaceViewModel? model)
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
        }

        _model = model;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelChanged;
        }
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs changed)
    {
        switch (changed.PropertyName)
        {
            case nameof(InterfaceViewModel.PadState):
            case nameof(InterfaceViewModel.IsConnected):
                Draw();
                break;
            case nameof(InterfaceViewModel.FaceLabels):
            case nameof(InterfaceViewModel.DPadLabels):
            case nameof(InterfaceViewModel.DPadRoles):
                Relabel();
                Draw();
                break;
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Relabel();

    private void Relabel()
    {
        if (_model is not { } model)
        {
            return;
        }

        var text = Localizer.Current;
        for (var index = 0; index < 4; index++)
        {
            var input = At(model.DPadLabels, index);
            Say(_dPad[index], input is null ? null : string.Format(CultureInfo.CurrentCulture, text.Text("pad.input"), input));
            Say(_face[index], At(model.FaceLabels, index));
        }

        Say(_leftStick, text.Text("surface.panTilt"));
        Say(_rightStick, text.Text("surface.zoomFocus"));
        Say(_leftShoulder, text.Text("pad.alt"));
        Say(_leftTrigger, text.Text("pad.altLower"));
        Say(_rightShoulder, text.Text("mixer.cut"));
        Say(_rightTrigger, text.Text("mixer.auto"));
    }

    private void Draw()
    {
        if (_model is not { } model)
        {
            return;
        }

        // A pad that is not there is drawn in faint lines rather than hidden, because the shape
        // says "unplugged" where an empty panel says nothing at all.
        Pad.Classes.Set("disconnected", !model.IsConnected);
        var state = model.IsConnected ? model.PadState : default;

        for (var index = 0; index < 4; index++)
        {
            var role = index < model.DPadRoles.Count ? model.DPadRoles[index] : InputRole.None;
            Light(_dPad[index], state.IsPressed(DPadButtons[index]), role);
            Light(_face[index], state.IsPressed(FaceButtons[index]));
        }

        Light(_leftShoulder, state.IsPressed(GamepadButtons.LeftShoulder));
        Light(_rightShoulder, state.IsPressed(GamepadButtons.RightShoulder));
        Pull(_leftTrigger, state.LeftTrigger, state.IsPressed(GamepadButtons.LeftTrigger));
        Pull(_rightTrigger, state.RightTrigger, state.IsPressed(GamepadButtons.RightTrigger));

        var left = StickOffset(state.LeftStick, isLeftStick: true);
        var right = StickOffset(state.RightStick, isLeftStick: false);
        Place(_leftKnob, LeftStickCentre, left);
        Place(_rightKnob, RightStickCentre, right);
        // On the pad the knob itself shows the stick moving, so only the legend lights for it.
        Light(_leftStick, Moved(left), onPad: false);
        Light(_rightStick, Moved(right), onPad: false);
    }

    private static IEnumerable<Part> Clockwise(Part[] cluster) => [cluster[0], cluster[3], cluster[1], cluster[2]];

    private static string? At(IReadOnlyList<string?> labels, int index) =>
        index < labels.Count ? labels[index] : null;

    private static bool Moved(Point offset) => Math.Abs(offset.X) > 0.01 || Math.Abs(offset.Y) > 0.01;

    /// A control is its glyph on the pad and its row in the legend, whose icon carries the same
    /// symbol and lights with it. The face is the shape a trigger's pull is drawn into.
    /// A pull runs across a trigger, from the middle of the edge it stands on to the middle of its
    /// top, which is the way a pull fills it.
    private sealed record Part(Canvas Glyph, Path Face, Canvas Icon, Border Row, TextBlock Label, PullAxis Pull);

    private readonly record struct PullAxis(Point Bottom, Point Top);

    private Part Add(((Canvas Glyph, Path Face) OnPad, PullAxis Pull) band, Canvas icon) =>
        Add(band.OnPad, icon, band.Pull);

    private Part Add((Canvas Glyph, Path Face) onPad, Canvas icon, PullAxis pull = default)
    {
        Pad.Children.Add(onPad.Glyph);
        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var row = new Border
        {
            Classes = { "row" },
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { new Viewbox { Child = icon }, label },
            },
        };
        return new Part(onPad.Glyph, onPad.Face, icon, row, label, pull);
    }

    private static void Say(Part part, string? text)
    {
        part.Label.Text = text;
        part.Row.IsVisible = !string.IsNullOrEmpty(text);
        part.Glyph.Classes.Set("unbound", !part.Row.IsVisible);
    }

    /// Being pressed lights the control, its icon and its legend row. A direction is also tinted by
    /// what its input is doing on the mixer.
    private static void Light(Part part, bool pressed, InputRole role = InputRole.None, bool onPad = true)
    {
        foreach (var glyph in onPad ? [part.Glyph, part.Icon] : new[] { part.Icon })
        {
            glyph.Classes.Set("pressed", pressed);
            glyph.Classes.Set("program", role == InputRole.Program);
            glyph.Classes.Set("preview", role == InputRole.Preview);
        }

        part.Row.Classes.Set("pressed", pressed);
    }

    /// A trigger is a switch with travel. It lights like every other button once the mapper has
    /// switched it, and until then a fill rising from the bottom says how far it has been pulled.
    private void Pull(Part trigger, double travel, bool switched)
    {
        Light(trigger, switched);
        var pulled = Math.Clamp(travel, 0, 1);
        if (switched || pulled <= 0)
        {
            trigger.Face.ClearValue(Avalonia.Controls.Shapes.Shape.FillProperty);
            return;
        }

        var pulling = Shade("PadPull");
        var resting = Shade("PadButton");
        trigger.Face.Fill = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(trigger.Pull.Bottom, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(trigger.Pull.Top, RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(pulling, 0),
                new GradientStop(pulling, pulled),
                new GradientStop(resting, pulled),
                new GradientStop(resting, 1),
            },
        };
    }

    private Color Shade(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var found) && found is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"the pad's theme has no {key}");

    private static void Place(Path knob, Point centre, Point offset)
    {
        Canvas.SetLeft(knob, centre.X + offset.X);
        Canvas.SetTop(knob, centre.Y + offset.Y);
        knob.Classes.Set("pressed", Moved(offset));
    }

    private static Path Knob() => new()
    {
        Data = Circle(default, KnobRadius),
        Classes = { "knob" },
        IsHitTestVisible = false,
    };

    private static Path Control(Geometry geometry) => new() { Data = geometry, Classes = { "control" } };

    /// One arm of the cross with an arrow pointing out of it. The arm is left open where it meets
    /// the hub, so that edge is filled but never stroked and the four arms read as one cross.
    private static (Canvas, Path) ArmGlyph(int index)
    {
        var d = Directions[index];
        var across = new Vector(d.Y, d.X);
        var inner = DPadCentre + (d * 7);
        var outer = DPadCentre + (d * 23);
        var tip = DPadCentre + (d * 19);
        var arm = Control(Geometry.Parse(
            $"M{At(inner + (across * 7))} L{At(tip + (across * 7))} Q{At(outer + (across * 7))} {At(outer + (across * 3))} " +
            $"L{At(outer - (across * 3))} Q{At(outer - (across * 7))} {At(tip - (across * 7))} L{At(inner - (across * 7))}"));
        return (new Canvas { Children = { arm, Arrow(DPadCentre + (d * 15), d, 4) } }, arm);
    }

    /// A direction in the legend: a key with the arm's arrow on it.
    private static Canvas KeyIcon(int index)
    {
        var centre = new Point(11, 11);
        return new Canvas
        {
            Width = 22,
            Height = 22,
            Children =
            {
                Control(new RectangleGeometry(new Rect(2, 2, 18, 18)) { RadiusX = 4, RadiusY = 4 }),
                Arrow(centre + (Directions[index] * 1.5), Directions[index], 5),
            },
            Classes = { "dpad" },
        };
    }

    private static Path Arrow(Point centre, Vector direction, double size)
    {
        var across = new Vector(direction.Y, direction.X);
        var tip = centre + (direction * size * 0.8);
        var back = centre - (direction * size * 0.6);
        return new Path
        {
            Data = Geometry.Parse($"M{At(tip)} L{At(back + (across * size))} L{At(back - (across * size))} Z"),
            Classes = { "arrow" },
        };
    }

    /// A face button with its symbol, in its colour.
    private static Canvas FaceGlyph(Point centre, int index)
    {
        var symbol = Symbols[index];
        var r = FaceRadius * 0.45;
        var mark = symbol switch
        {
            "triangle" => $"M{At(centre + new Vector(0, -r * 1.1))} L{At(centre + new Vector(r, r * 0.75))} L{At(centre + new Vector(-r, r * 0.75))} Z",
            "cross" => $"M{At(centre + new Vector(-r, -r))} L{At(centre + new Vector(r, r))} M{At(centre + new Vector(r, -r))} L{At(centre + new Vector(-r, r))}",
            "square" => $"M{At(centre + new Vector(-r * 0.85, -r * 0.85))} H{Num(centre.X + (r * 0.85))} V{Num(centre.Y + (r * 0.85))} H{Num(centre.X - (r * 0.85))} Z",
            _ => null,
        };
        return new Canvas
        {
            Width = 22,
            Height = 22,
            Classes = { symbol },
            Children =
            {
                Control(Circle(centre, FaceRadius)),
                new Path
                {
                    Data = mark is null ? Circle(centre, r) : Geometry.Parse(mark),
                    Classes = { "mark", symbol },
                },
            },
        };
    }

    private static Canvas StickGlyph(Point centre, double radius, string? letter)
    {
        var glyph = new Canvas { Width = 22, Height = 22, Children = { Control(Circle(centre, radius)) } };
        if (letter is not null)
        {
            glyph.Children.Add(Mark(letter, centre, 12));
        }

        return glyph;
    }

    /// A glyph placed on the pad, paired with the shape that carries its fill. Every glyph is built
    /// with that shape first.
    private static (Canvas, Path) OnPad(Canvas glyph) =>
        (glyph, glyph.Children.OfType<Path>().First());

    /// A shoulder control on the pad: the band between two cubic edges, named at its middle. The
    /// lower edge is shared with whatever it stands on, so only the upper corners are rounded. The
    /// right one is the mirror image of the left.
    private static ((Canvas, Path) OnPad, PullAxis Pull) Band(
        Point[] lower, Point[] upper, double radius, string name, bool mirrored)
    {
        if (mirrored)
        {
            lower = [.. lower.Select(Mirror)];
            upper = [.. upper.Select(Mirror)];
        }

        var inset = radius / Length(upper);
        var top = Sub(upper, inset, 1 - inset);
        var end = upper[3] + (Unit(lower[3] - upper[3]) * radius);
        var start = upper[0] + (Unit(lower[0] - upper[0]) * radius);
        // The figure is left open along the lower edge, so that edge is drawn once, by whatever the
        // band stands on, rather than twice in two shades. Its fill closes along the chord, which
        // lies inside the shape drawn over it.
        var shape = Control(Geometry.Parse(
            $"M{At(lower[3])} L{At(end)} Q{At(upper[3])} {At(top[3])} C{At(top[2])} {At(top[1])} {At(top[0])} " +
            $"Q{At(upper[0])} {At(start)} L{At(lower[0])}"));
        var bottom = Sub(lower, 0, 0.5);
        var crown = Sub(upper, 0, 0.5)[3];
        var middle = bottom[3] + ((crown - bottom[3]) / 2);

        // The name runs along the band, read left to right on either side.
        var along = bottom[3] - bottom[2];
        var angle = Math.Atan2(along.Y, along.X) * 180 / Math.PI;
        var mark = Mark(name, middle, 7);
        mark.RenderTransform = new RotateTransform(along.X < 0 ? angle + 180 : angle);
        return ((new Canvas { Children = { shape, mark } }, shape), new PullAxis(bottom[3], crown));
    }

    /// The stretch of a cubic between two of its parameters, by de Casteljau.
    private static Point[] Sub(Point[] curve, double from, double to)
    {
        var head = Split(curve, to).Head;
        return from <= 0 ? head : Split(head, from / to).Tail;
    }

    private static (Point[] Head, Point[] Tail) Split(Point[] c, double t)
    {
        Point Lerp(Point a, Point b) => a + ((b - a) * t);
        var ab = Lerp(c[0], c[1]);
        var bc = Lerp(c[1], c[2]);
        var cd = Lerp(c[2], c[3]);
        var abc = Lerp(ab, bc);
        var bcd = Lerp(bc, cd);
        var middle = Lerp(abc, bcd);
        return ([c[0], ab, abc, middle], [middle, bcd, cd, c[3]]);
    }

    /// A cubic moved outward, away from the body, each end along its own normal. Only an
    /// approximation of a true offset curve, which is fine for the band's free edge.
    private static Point[] Offset(Point[] c, double distance, double dome = 0)
    {
        var start = Outward(c[1] - c[0]);
        var end = Outward(c[3] - c[2]);
        return
        [
            c[0] + (start * distance),
            c[1] + (start * (distance + dome)),
            c[2] + (end * (distance + dome)),
            c[3] + (end * distance),
        ];
    }

    /// The length of a cubic's control polygon, which bounds the curve's own and is close enough to
    /// size a corner by.
    private static double Length(Point[] c) => Point.Distance(c[0], c[1]) + Point.Distance(c[1], c[2]) + Point.Distance(c[2], c[3]);

    private static Vector Unit(Vector v) => v / v.Length;

    /// The corner runs right to left and down, so outward is the tangent turned a quarter clockwise.
    private static Vector Outward(Vector tangent) => new Vector(-tangent.Y, tangent.X) / tangent.Length;

    /// A shoulder control in the legend: a bumper is a wide tab, a trigger a taller one.
    private static Canvas TabIcon(string name, bool tall)
    {
        // The trigger is drawn as the taller, domed key it is on the pad.
        var face = tall
            ? Geometry.Parse("M2,21 L2,9 Q2,1 14,1 Q26,1 26,9 L26,21 Z")
            : new RectangleGeometry(new Rect(1, 5, 26, 13)) { RadiusX = 5, RadiusY = 5 };
        return new Canvas
        {
            Width = 28,
            Height = 22,
            Children = { Control(face), Mark(name, new Point(14, tall ? 12.5 : 11.5), 11) },
        };
    }

    /// A name centred on a point: a box centred there, the text centred in the box, so it sits
    /// in the middle of its button whatever the font's metrics, and turns about that middle.
    private static Panel Mark(string text, Point centre, double size)
    {
        var box = new Panel
        {
            Width = 4 * size,
            Height = 2 * size,
            Children =
            {
                new TextBlock
                {
                    Text = text,
                    FontSize = size,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Classes = { "mark" },
                },
            },
        };
        Canvas.SetLeft(box, centre.X - (2 * size));
        Canvas.SetTop(box, centre.Y - size);
        return box;
    }

    private static Point Mirror(Point point) => new(PadWidth - point.X, point.Y);

    private static string At(Point point) => $"{Num(point.X)},{Num(point.Y)}";

    private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static Geometry Circle(Point centre, double radius) =>
        new EllipseGeometry(new Rect(centre.X - radius, centre.Y - radius, 2 * radius, 2 * radius));

    private static Geometry Square(Point centre, double half) =>
        new RectangleGeometry(new Rect(centre.X - half, centre.Y - half, 2 * half, 2 * half));
}
