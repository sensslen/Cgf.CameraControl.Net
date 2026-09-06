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
    private readonly BehaviorSubject<bool> _mixerConnected = new(false);
    private readonly Dictionary<int, ICameraConnection> _cameras = [];
    private readonly Dictionary<int, BehaviorSubject<bool>> _cameraConnected = [];

    protected GamepadTests()
    {
        _mixer.WhenPreviewChanged.Returns(_preview);
        _mixer.WhenProgramChanged.Returns(_program);
        _mixer.WhenConnectedChanged.Returns(_mixerConnected);
        _mixer.ConnectionString.Returns("mixer");
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

    public class Rumble : GamepadTests
    {
        [Fact]
        public void ATransitionIsAcknowledged()
        {
            Build();

            _device.RequestTransition(MixerTransition.Cut);

            Assert.Single(_device.Rumbles);
        }

        [Fact]
        public void ATransitionThatWasRefusedIsNotAcknowledged()
        {
            Build(enableChangingProgram: false);

            _device.RequestTransition(MixerTransition.Cut);

            Assert.Empty(_device.Rumbles);
        }

        [Fact]
        public void OneOfOurCamerasGoingOnAirIsFelt()
        {
            Camera(1);
            Build();

            _program.OnNext(1);

            Assert.Single(_device.Rumbles);
        }

        [Fact]
        public void ProgramMovingToSomethingWeDoNotSteerIsNotFelt()
        {
            Camera(1);
            Build();

            _program.OnNext(9);

            Assert.Empty(_device.Rumbles);
        }

        [Fact]
        public void LosingTheMixerIsFelt()
        {
            Build();
            _mixerConnected.OnNext(true);

            _mixerConnected.OnNext(false);

            Assert.Single(_device.Rumbles);
        }

        // Every connection starts disconnected, and an interface built before the switcher answers
        // must not buzz in the operator's hand on startup.
        [Fact]
        public void StartingUpDisconnectedIsNotFelt()
        {
            Camera(1);
            Build();

            Assert.Empty(_device.Rumbles);
        }

        [Fact]
        public void LosingTheSelectedCameraIsFelt()
        {
            Camera(1);
            Build();
            _preview.OnNext(new PreviewChange(1, false));
            _cameraConnected[1].OnNext(true);

            _cameraConnected[1].OnNext(false);

            Assert.Single(_device.Rumbles);
        }

        // Another desk's camera dropping is not this operator's problem, and a pad that buzzes for
        // everything stops meaning anything.
        [Fact]
        public void LosingACameraNobodyHereSteersIsNotFelt()
        {
            Camera(1);
            Camera(2);
            Build();
            _preview.OnNext(new PreviewChange(1, false));
            _cameraConnected[2].OnNext(true);

            _cameraConnected[2].OnNext(false);

            Assert.Empty(_device.Rumbles);
        }

        [Fact]
        public void APadThatWillNotRumbleStaysSilent()
        {
            Build(rumble: false);

            _device.RequestTransition(MixerTransition.Cut);

            Assert.Empty(_device.Rumbles);
        }
    }

    private ICameraConnection Camera(int instance)
    {
        var connected = new BehaviorSubject<bool>(false);
        var camera = Substitute.For<ICameraConnection>();
        camera.ConnectionString.Returns($"camera-{instance}");
        camera.WhenConnectedChanged.Returns(connected);
        _cameras[instance] = camera;
        _cameraConnected[instance] = connected;
        return camera;
    }

    private Gamepad Build(bool enableChangingProgram = true, bool rumble = true)
    {
        _device.SupportsRumble = rumble;
        return new Gamepad(
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
                Functions = new Dictionary<string, SpecialFunctionConfiguration>
                {
                    ["iso"] = new KeySpecialFunctionConfiguration { Index = 1 },
                    ["lower"] = new KeySpecialFunctionConfiguration { Index = 9 },
                },
            },
            _device,
            _mixer,
            instance => _cameras.GetValueOrDefault(instance),
            _logger,
            new PadBindings
            {
                Default = new Dictionary<ButtonDirection, string> { [ButtonDirection.Down] = "iso" },
                Alt = new Dictionary<ButtonDirection, string> { [ButtonDirection.Down] = "lower" },
            });
    }
}
