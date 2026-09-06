using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Media;
using Cgf.CameraControl.App.ViewModels;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.App.Views;

/// The pad as the application sees it, an outline with the live state laid over it. Only the
/// controls an interface reads are lit: a shape that can never light up is a bug report waiting to
/// happen.
public partial class GamepadWireframe : UserControl
{
    private static readonly IBrush Lit = new SolidColorBrush(Color.FromRgb(0x4A, 0x9E, 0xE0));
    private static readonly IBrush Resting = new SolidColorBrush(Color.FromArgb(0x00, 0, 0, 0));
    private static readonly IBrush Knob = new SolidColorBrush(Color.FromArgb(0xB0, 0x8A, 0x8A, 0x8A));
    private static readonly Color Pulling = Color.FromArgb(0x38, 0x4A, 0x9E, 0xE0);

    /// The two colours a vision mixer is read in, softened because these say what an input is doing,
    /// not that somebody is pressing it.
    private static readonly IBrush OnPreview = new SolidColorBrush(Color.FromArgb(0x90, 0x2E, 0xCC, 0x71));
    private static readonly IBrush OnProgram = new SolidColorBrush(Color.FromArgb(0x90, 0xE7, 0x4C, 0x3C));

    /// Where each stick's knob rests, and how far it travels, in the drawing's own coordinates. The
    /// travel keeps the knob inside its ring at full deflection, as a real stick stays in its well.
    private const double LeftStickX = 123.4;
    private const double RightStickX = 207.2;
    private const double StickY = 162.2;
    private const double Travel = 9;

    public GamepadWireframe()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Follow();
        Follow();
    }

    private InterfaceViewModel? Model => DataContext as InterfaceViewModel;

    private void Follow()
    {
        if (Model is not { } model)
        {
            return;
        }

        model.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName is nameof(InterfaceViewModel.PadState)
                or nameof(InterfaceViewModel.FaceLabels)
                or nameof(InterfaceViewModel.DPadLabels)
                or nameof(InterfaceViewModel.DPadRoles)
                or nameof(InterfaceViewModel.IsConnected))
            {
                Draw();
            }
        };

        Draw();
    }

    private void Draw()
    {
        if (Model is not { } model)
        {
            return;
        }

        // A pad that is not there is drawn rather than hidden, because the shape beside a dead lamp
        // says "unplugged" where an empty panel says nothing at all.
        Opacity = model.IsConnected ? 1 : 0.4;
        var state = model.IsConnected ? model.PadState : default;

        // A direction is tinted by what its input is doing and filled by the thumb on it, so being
        // pressed always wins over being live.
        Light(DPadUp, state, GamepadButtons.DPadUp, Role(model, 0));
        Light(DPadDown, state, GamepadButtons.DPadDown, Role(model, 1));
        Light(DPadLeft, state, GamepadButtons.DPadLeft, Role(model, 2));
        Light(DPadRight, state, GamepadButtons.DPadRight, Role(model, 3));
        Light(FaceUp, state, GamepadButtons.FaceUp);
        Light(FaceDown, state, GamepadButtons.FaceDown);
        Light(FaceLeft, state, GamepadButtons.FaceLeft);
        Light(FaceRight, state, GamepadButtons.FaceRight);

        Press(LeftShoulder, state.IsPressed(GamepadButtons.LeftShoulder));
        Press(RightShoulder, state.IsPressed(GamepadButtons.RightShoulder));

        Pull(LeftTrigger, state.LeftTrigger, state.IsPressed(GamepadButtons.LeftTrigger));
        Pull(RightTrigger, state.RightTrigger, state.IsPressed(GamepadButtons.RightTrigger));

        Place(LeftKnob, LeftStickX, state.LeftStick);
        Place(RightKnob, RightStickX, state.RightStick);

        Say(DPadUpPlate, DPadUpLabel, model.DPadLabels, 0);
        Say(DPadDownPlate, DPadDownLabel, model.DPadLabels, 1);
        Say(DPadLeftPlate, DPadLeftLabel, model.DPadLabels, 2);
        Say(DPadRightPlate, DPadRightLabel, model.DPadLabels, 3);
        Say(FaceUpPlate, FaceUpLabel, model.FaceLabels, 0);
        Say(FaceDownPlate, FaceDownLabel, model.FaceLabels, 1);
        Say(FaceLeftPlate, FaceLeftLabel, model.FaceLabels, 2);
        Say(FaceRightPlate, FaceRightLabel, model.FaceLabels, 3);
    }

    private static InputRole Role(InterfaceViewModel model, int index) =>
        index < model.DPadRoles.Count ? model.DPadRoles[index] : InputRole.None;

    private static void Light(
        Shape shape,
        GamepadState state,
        GamepadButtons button,
        InputRole role = InputRole.None) =>
        shape.Fill = state.IsPressed(button)
            ? Lit
            : role switch
            {
                InputRole.Program => OnProgram,
                InputRole.Preview => OnPreview,
                _ => Resting,
            };

    private static void Press(Shape bumper, bool held) => bumper.Fill = held ? Lit : Resting;

    /// A trigger is a switch with travel. It lights like every other button once the mapper has
    /// switched it, and until then only a faint fill rising from the bottom says how far it has been
    /// pulled, faint because a half-pulled trigger has done nothing yet and must not look as if it had.
    private static void Pull(Path shield, double travel, bool switched)
    {
        var pulled = Math.Clamp(travel, 0, 1);
        shield.Fill = switched
            ? Lit
            : pulled <= 0
                ? Resting
                : new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Pulling, 0),
                        new GradientStop(Pulling, pulled),
                        new GradientStop(Colors.Transparent, pulled),
                        new GradientStop(Colors.Transparent, 1),
                    },
                };
    }

    private static void Place(Ellipse knob, double restingX, StickPosition at)
    {
        knob.Fill = Math.Abs(at.X) > 0.001 || Math.Abs(at.Y) > 0.001 ? Lit : Knob;
        Canvas.SetLeft(knob, restingX + (at.X * Travel) - (knob.Width / 2));
        Canvas.SetTop(knob, StickY - (at.Y * Travel) - (knob.Height / 2));
    }

    /// An unbound control carries no label, which is what makes the drawing say the same thing the
    /// configuration file does.
    private static void Say(Border plate, TextBlock label, IReadOnlyList<string?> labels, int index)
    {
        label.Text = index < labels.Count ? labels[index] : null;
        plate.IsVisible = !string.IsNullOrEmpty(label.Text);
    }
}
