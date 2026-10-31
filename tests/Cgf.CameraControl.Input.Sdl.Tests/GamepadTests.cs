using System.Reactive.Subjects;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Core.VideoMixer.Passthrough;
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

        [Fact]
        public async Task TearingDownStopsTheCameraBeingSteered()
        {
            var camera = Camera(1);
            var pad = Build();
            _preview.OnNext(new PreviewChange(1, false));
            _device.MoveLeftStick(1, 1);

            await pad.DisposeAsync();

            camera.Received(1).Pan(0);
            camera.Received(1).Tilt(0);
            camera.Received(1).Zoom(0);
            camera.Received(1).Focus(0);
        }

        [Fact]
        public async Task StickEventsDuringShutdownCannotOverwriteTheStop()
        {
            var camera = Camera(1);
            var pad = Build();
            _preview.OnNext(new PreviewChange(1, false));
            _device.MoveLeftStick(1, 1);
            camera.When(c => c.Zoom(0)).Do(_ => _device.MoveLeftStick(0.75, 0.5));
            camera.When(c => c.Focus(0)).Do(_ => _device.MoveRightStick(0.75, 0.5));

            await pad.DisposeAsync();

            camera.DidNotReceive().Pan(0.75);
            camera.DidNotReceive().Tilt(0.5);
            camera.DidNotReceive().Zoom(0.5);
            camera.DidNotReceive().Focus(0.75);
        }

        [Fact]
        public async Task ShutdownWaitsForInFlightSteeringBeforeSendingTheFinalStop()
        {
            var camera = Camera(1);
            var pad = Build();
            _preview.OnNext(new PreviewChange(1, false));
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var commands = new List<double>();
            var tiltCommands = new List<double>();
            camera.When(c => c.Pan(Arg.Any<double>())).Do(call => commands.Add(call.Arg<double>()));
            camera.When(c => c.Tilt(Arg.Any<double>())).Do(call => tiltCommands.Add(call.Arg<double>()));
            camera.When(c => c.Pan(0.5)).Do(_ =>
            {
                entered.SetResult();
                resume.Task.WaitAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();
            });
            var steering = Task.Run(() => _device.MoveLeftStick(0.5, 0.5), TestContext.Current.CancellationToken);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            var shutdown = Task.Run(async () => await pad.DisposeAsync(), TestContext.Current.CancellationToken);
            resume.SetResult();

            await Task.WhenAll(steering, shutdown)
                .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

            Assert.Equal(new[] { 0.5, 0.0 }, commands);
            Assert.Equal(new[] { 0.5, 0.0 }, tiltCommands);
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

    public class MixerTallyPolicy : GamepadTests
    {
        [Theory]
        [InlineData(MixerTallyMode.Program)]
        [InlineData(MixerTallyMode.None)]
        public void RestrictedModesClearAllMappedCamerasAtStartup(MixerTallyMode mode)
        {
            var first = Camera(1);
            var second = Camera(2);
            _mixer.TallyMode.Returns(mode);

            Build();

            first.Received(1).SetTally(TallyState.Off);
            second.Received(1).SetTally(TallyState.Off);
        }

        [Fact]
        public void ProgramOnlyTurnsOffPreviewAndTheCameraLeavingProgram()
        {
            var first = Camera(1);
            var second = Camera(2);
            _mixer.TallyMode.Returns(MixerTallyMode.Program);
            Build();

            _preview.OnNext(new PreviewChange(1, false));
            _program.OnNext(1);
            _preview.OnNext(new PreviewChange(2, false));
            _program.OnNext(2);

            first.Received(1).SetTally(TallyState.Program);
            first.Received(3).SetTally(TallyState.Off);
            second.Received(1).SetTally(TallyState.Program);
            first.DidNotReceive().SetTally(TallyState.Preview);
            second.DidNotReceive().SetTally(TallyState.Preview);
        }

        [Fact]
        public void ProgramOnlyKeepsTheLiveCameraLitWhenPreviewChanges()
        {
            var first = Camera(1);
            Camera(2);
            _mixer.TallyMode.Returns(MixerTallyMode.Program);
            Build();
            _program.OnNext(1);
            first.ClearReceivedCalls();

            _preview.OnNext(new PreviewChange(1, true));
            _preview.OnNext(new PreviewChange(2, false));

            first.DidNotReceive().SetTally(TallyState.Off);
            first.DidNotReceive().SetTally(TallyState.Preview);
        }

        [Fact]
        public void NoTallySendsOffForBothBusesInsteadOfLeavingOldLampsLit()
        {
            var first = Camera(1);
            var second = Camera(2);
            _mixer.TallyMode.Returns(MixerTallyMode.None);
            Build();
            first.ClearReceivedCalls();
            second.ClearReceivedCalls();

            _preview.OnNext(new PreviewChange(1, false));
            _program.OnNext(1);
            _preview.OnNext(new PreviewChange(2, false));
            _program.OnNext(2);

            first.Received().SetTally(TallyState.Off);
            second.Received().SetTally(TallyState.Off);
            first.DidNotReceive().SetTally(TallyState.Preview);
            first.DidNotReceive().SetTally(TallyState.Program);
            second.DidNotReceive().SetTally(TallyState.Preview);
            second.DidNotReceive().SetTally(TallyState.Program);
            Assert.Equal(2, _device.Rumbles.Count);
        }
    }

    public class TallyAcrossMixers : GamepadTests
    {
        [Theory]
        [InlineData(TallyState.Off, TallyState.Off, TallyState.Off)]
        [InlineData(TallyState.Off, TallyState.Preview, TallyState.Preview)]
        [InlineData(TallyState.Off, TallyState.Program, TallyState.Program)]
        [InlineData(TallyState.Preview, TallyState.Off, TallyState.Preview)]
        [InlineData(TallyState.Preview, TallyState.Preview, TallyState.Preview)]
        [InlineData(TallyState.Preview, TallyState.Program, TallyState.Program)]
        [InlineData(TallyState.Program, TallyState.Off, TallyState.Program)]
        [InlineData(TallyState.Program, TallyState.Preview, TallyState.Program)]
        [InlineData(TallyState.Program, TallyState.Program, TallyState.Program)]
        public async Task HighestPriorityWins(TallyState first, TallyState second, TallyState expected)
        {
            var camera = Camera(1);
            var displayed = TallyState.Off;
            camera.When(c => c.SetTally(Arg.Any<TallyState>())).Do(call => displayed = call.Arg<TallyState>());
            await using var primary = Build();
            await using var mixer = new Passthrough(_logger);
            await using var secondary = OtherInterface(mixer);
            _preview.OnNext(new PreviewChange(1, false));
            _program.OnNext(first == TallyState.Program ? 1 : -1);
            if (first == TallyState.Off)
            {
                _preview.OnNext(new PreviewChange(9, false));
            }

            mixer.ChangeInput(1);
            if (second == TallyState.Program)
            {
                mixer.Cut();
            }
            else if (second == TallyState.Off)
            {
                mixer.ChangeInput(9);
            }

            Assert.Equal(expected, displayed);
        }

        [Fact]
        public async Task LeavingProgramFallsBackToAnotherMixersPreviewThenOff()
        {
            var camera = Camera(1);
            var displayed = new List<TallyState>();
            camera.When(c => c.SetTally(Arg.Any<TallyState>())).Do(call => displayed.Add(call.Arg<TallyState>()));
            await using var primary = Build();
            await using var mixer = new Passthrough(_logger);
            await using var secondary = OtherInterface(mixer);
            _program.OnNext(1);
            mixer.ChangeInput(1);

            Assert.All(displayed, state => Assert.Equal(TallyState.Program, state));
            _program.OnNext(9);
            Assert.Equal(TallyState.Preview, displayed[^1]);
            mixer.ChangeInput(9);
            Assert.Equal(TallyState.Off, displayed[^1]);
        }

        [Theory]
        [InlineData(MixerTallyMode.Program)]
        [InlineData(MixerTallyMode.None)]
        public async Task SuppressedSignalsAndStartupCannotClearAnotherMixersProgram(MixerTallyMode mode)
        {
            var camera = Camera(1);
            await using var primary = Build();
            _program.OnNext(1);
            camera.ClearReceivedCalls();

            await using var mixer = new Passthrough(_logger, mode);
            await using var secondary = OtherInterface(mixer);
            mixer.ChangeInput(1);
            mixer.Cut();
            mixer.ChangeInput(9);
            mixer.Cut();

            camera.Received().SetTally(TallyState.Program);
            camera.DidNotReceive().SetTally(TallyState.Preview);
            camera.DidNotReceive().SetTally(TallyState.Off);
        }

        [Fact]
        public async Task RemovingAnInterfaceRecomputesTheRemainingMixersTally()
        {
            var camera = Camera(1);
            var displayed = TallyState.Off;
            camera.When(c => c.SetTally(Arg.Any<TallyState>())).Do(call => displayed = call.Arg<TallyState>());
            var primary = Build();
            await using var mixer = new Passthrough(_logger);
            var secondary = OtherInterface(mixer);
            mixer.ChangeInput(1);
            _program.OnNext(1);

            await primary.DisposeAsync();
            Assert.Equal(TallyState.Preview, displayed);
            await secondary.DisposeAsync();
            Assert.Equal(TallyState.Off, displayed);

            _program.OnNext(1);
            mixer.Cut();
            Assert.Equal(TallyState.Off, displayed);
        }

        [Fact]
        public async Task RemovingOneOfTwoInterfacesOnTheSameMixerKeepsItsTally()
        {
            var camera = Camera(1);
            var primary = Build();
            var secondary = OtherInterface(_mixer);
            _program.OnNext(1);
            camera.ClearReceivedCalls();

            await primary.DisposeAsync();
            camera.Received().SetTally(TallyState.Program);
            camera.DidNotReceive().SetTally(TallyState.Off);
            await secondary.DisposeAsync();
            camera.Received(1).SetTally(TallyState.Off);
        }

        private Gamepad OtherInterface(IVideoMixer mixer) =>
            new(
                new KeyboardConfiguration
                {
                    VideoMixer = 2,
                    CameraMap = _cameras.Keys.ToDictionary(key => key, key => key),
                    ConnectionChange = new DirectConnectionChangeConfiguration
                    {
                        Default = new Dictionary<ButtonDirection, int>(),
                    },
                },
                new ControlSurfaceDevice(2),
                mixer,
                instance => _cameras.GetValueOrDefault(instance),
                _logger);
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
