using System.Reactive.Subjects;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;
using NSubstitute;

namespace Cgf.CameraControl.Input.Sdl.Tests;

public class GamepadTests
{
    private readonly FakeGamepadDevice _device = new();
    private readonly IVideoMixer _mixer = Substitute.For<IVideoMixer>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly Subject<PreviewChange> _preview = new();
    private readonly Subject<int> _program = new();
    private readonly Dictionary<int, ICameraConnection> _cameras = [];

    protected GamepadTests()
    {
        _mixer.WhenPreviewChanged.Returns(_preview);
        _mixer.WhenProgramChanged.Returns(_program);
    }

    public class Steering : GamepadTests
    {
        [Fact]
        public void TheSticksDriveWhicheverCameraIsOnPreview()
        {
            var camera = Camera(1);
            Build();
            _preview.OnNext(new PreviewChange(1, false));

            _device.MoveLeftStick(0.5, -0.25);
            _device.MoveRightStick(0.75, 1);

            camera.Received(1).Pan(0.5);
            camera.Received(1).Tilt(-0.25);
            camera.Received(1).Focus(0.75);
            camera.Received(1).Zoom(1);
        }

        [Fact]
        public void NothingMovesWhilePreviewIsNotACamera()
        {
            var camera = Camera(1);
            Build();

            _preview.OnNext(new PreviewChange(9, false));
            _device.MoveLeftStick(1, 1);

            camera.DidNotReceive().Pan(Arg.Any<double>());
        }

        // The sticks are almost never centred at the moment of a cut, so a camera left behind would
        // keep moving with nobody steering it.
        [Fact]
        public void SwitchingPreviewStopsTheCameraBeingLeftBehind()
        {
            var first = Camera(1);
            Camera(2);
            Build();
            _preview.OnNext(new PreviewChange(1, false));
            _device.MoveLeftStick(1, 1);

            _preview.OnNext(new PreviewChange(2, false));

            first.Received(1).Pan(0);
            first.Received(1).Tilt(0);
            first.Received(1).Zoom(0);
            first.Received(1).Focus(0);
        }
    }

    public class Tally : GamepadTests
    {
        [Fact]
        public void ThePreviewCameraLightsPreviewAndTheOldOneGoesOut()
        {
            var first = Camera(1);
            var second = Camera(2);
            Build();

            _preview.OnNext(new PreviewChange(1, false));
            _preview.OnNext(new PreviewChange(2, false));

            first.Received(1).SetTally(TallyState.Preview);
            first.Received(1).SetTally(TallyState.Off);
            second.Received(1).SetTally(TallyState.Preview);
        }

        [Fact]
        public void TheProgramCameraLightsProgram()
        {
            var camera = Camera(3);
            Build();

            _program.OnNext(3);

            camera.Received(1).SetTally(TallyState.Program);
        }

        // Preview and program on one camera must not leave it dark: program wins and it is never
        // told to go off while it is still live.
        [Fact]
        public void ACameraOnBothBusesStaysLitAsProgram()
        {
            var camera = Camera(1);
            Build();
            _preview.OnNext(new PreviewChange(1, true));

            _program.OnNext(1);

            camera.Received(1).SetTally(TallyState.Program);
            camera.DidNotReceive().SetTally(TallyState.Off);
        }

        [Fact]
        public void LeavingProgramReturnsTheStillSelectedCameraToPreview()
        {
            var first = Camera(1);
            var second = Camera(2);
            Build();
            _preview.OnNext(new PreviewChange(1, true));
            _program.OnNext(1);

            _program.OnNext(2);

            first.Received(2).SetTally(TallyState.Preview);
            second.Received(1).SetTally(TallyState.Program);
        }
    }

    public class Buttons : GamepadTests
    {
        [Fact]
        public void TheDirectionPadSelectsThroughTheConnectionChange()
        {
            Build();

            _device.PressDirection(ButtonDirection.Right);

            _mixer.Received(1).ChangeInput(2);
        }

        [Fact]
        public void HoldingAltSelectsFromTheAlternateSet()
        {
            Build();

            _device.HoldAlt();
            _device.PressDirection(ButtonDirection.Right);

            _mixer.Received(1).ChangeInput(7);
        }

        [Fact]
        public void ReleasingTheModifierRestoresTheDefaultSet()
        {
            Build();

            _device.HoldAlt();
            _device.ReleaseModifiers();
            _device.PressDirection(ButtonDirection.Right);

            _mixer.Received(1).ChangeInput(2);
        }

        [Fact]
        public void AFaceButtonRunsItsSpecialFunction()
        {
            Build();

            _device.PressFace(ButtonDirection.Down);

            _mixer.Received(1).ToggleKey(1);
        }

        [Fact]
        public void AModifierPicksTheAlternateSpecialFunction()
        {
            Build();

            _device.HoldAlt();
            _device.PressFace(ButtonDirection.Down);

            _mixer.Received(1).ToggleKey(9);
        }

        // Only some buttons are rebound under a modifier, so the rest have to keep working.
        [Fact]
        public void AModifierWithNothingBoundForThatButtonUsesTheDefault()
        {
            Build();

            _device.HoldAltLower();
            _device.PressFace(ButtonDirection.Down);

            _mixer.Received(1).ToggleKey(1);
        }
    }

    public class Transitions : GamepadTests
    {
        [Fact]
        public void CutAndAutoReachTheMixer()
        {
            Build();

            _device.RequestTransition(MixerTransition.Cut);
            _device.RequestTransition(MixerTransition.Auto);

            _mixer.Received(1).Cut();
            _mixer.Received(1).Auto();
        }

        // ensi.json runs one of its two desks with this off.
        [Fact]
        public void AnOperatorWithoutProgramRightsCannotTransition()
        {
            Build(enableChangingProgram: false);

            _device.RequestTransition(MixerTransition.Cut);
            _device.RequestTransition(MixerTransition.Auto);

            _mixer.DidNotReceive().Cut();
            _mixer.DidNotReceive().Auto();
        }

        [Fact]
        public void SelectingPreviewStillWorksWithoutProgramRights()
        {
            Build(enableChangingProgram: false);

            _device.PressDirection(ButtonDirection.Right);

            _mixer.Received(1).ChangeInput(2);
        }
    }

    private ICameraConnection Camera(int instance)
    {
        var camera = Substitute.For<ICameraConnection>();
        camera.ConnectionString.Returns($"camera-{instance}");
        _cameras[instance] = camera;
        return camera;
    }

    private Gamepad Build(bool enableChangingProgram = true) =>
        new(
            new GamepadConfiguration
            {
                VideoMixer = 1,
                EnableChangingProgram = enableChangingProgram,
                CameraMap = _cameras.Keys.ToDictionary(key => key, key => key),
                ConnectionChange = new DirectConnectionChangeConfiguration
                {
                    Default = new Dictionary<ButtonDirection, int> { [ButtonDirection.Right] = 2 },
                    Alt = new Dictionary<ButtonDirection, int> { [ButtonDirection.Right] = 7 },
                },
                SpecialFunction = new SpecialFunctionSet
                {
                    Default = new Dictionary<ButtonDirection, SpecialFunctionConfiguration>
                    {
                        [ButtonDirection.Down] = new KeySpecialFunctionConfiguration { Index = 1 },
                    },
                    Alt = new Dictionary<ButtonDirection, SpecialFunctionConfiguration>
                    {
                        [ButtonDirection.Down] = new KeySpecialFunctionConfiguration { Index = 9 },
                    },
                },
            },
            _device,
            _mixer,
            instance => _cameras.GetValueOrDefault(instance),
            _logger);
}
