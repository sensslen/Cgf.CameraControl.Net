using System.Reactive.Linq;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.Input.Sdl.Tests;

public class ControlSurfaceDeviceTests
{
    public class OnItsOwn
    {
        private readonly ControlSurfaceDevice _device = new(1);

        [Fact]
        public void MovementIsPanOnXAndTiltOnY()
        {
            var seen = new List<StickPosition>();
            using var subscription = _device.LeftStick.Subscribe(seen.Add);

            _device.Move(0.5, -0.25);

            Assert.Equal(new StickPosition(0.5, -0.25), Assert.Single(seen));
        }

        // The right stick is focus on X and zoom on Y, and swapping them is invisible on screen and
        // wrong on the camera.
        [Fact]
        public void TheLensIsFocusOnXAndZoomOnY()
        {
            var seen = new List<StickPosition>();
            using var subscription = _device.RightStick.Subscribe(seen.Add);

            _device.Lens(-1, 0.75);

            Assert.Equal(new StickPosition(-1, 0.75), Assert.Single(seen));
        }

        [Fact]
        public void ButtonsAndTransitionsAreReportedAsAPadReportsThem()
        {
            var selected = new List<ButtonDirection>();
            var ran = new List<ButtonDirection>();
            var transitions = new List<MixerTransition>();
            using var one = _device.ConnectionChangeRequested.Subscribe(selected.Add);
            using var two = _device.SpecialFunctionRequested.Subscribe(ran.Add);
            using var three = _device.TransitionRequested.Subscribe(transitions.Add);

            _device.Select(ButtonDirection.Left);
            _device.Run(ButtonDirection.Up);
            _device.Transition(MixerTransition.Auto);

            Assert.Equal(ButtonDirection.Left, Assert.Single(selected));
            Assert.Equal(ButtonDirection.Up, Assert.Single(ran));
            Assert.Equal(MixerTransition.Auto, Assert.Single(transitions));
        }

        // Gamepad reads the modifiers when it subscribes, so a surface that only reported changes
        // would leave it holding nothing until the first one was pressed.
        [Fact]
        public void TheModifiersAreReadableBeforeAnyAreHeld()
        {
            AltKeyConfiguration? seen = null;
            using var subscription = _device.Modifiers.Subscribe(value => seen = value);

            Assert.Equal(AltKeyConfiguration.None, seen);
        }

        [Fact]
        public void HoldingTheSameModifiersAgainReportsNothingNew()
        {
            var seen = new List<AltKeyConfiguration>();
            using var subscription = _device.Modifiers.Subscribe(seen.Add);

            _device.SetModifiers(alt: true, altLower: false);
            _device.SetModifiers(alt: true, altLower: false);

            Assert.Equal(2, seen.Count);
        }

        [Fact]
        public void AWindowIsConnectedBecauseAWindowIsNotUnplugged()
        {
            var connected = false;
            using var subscription = _device.WhenConnectedChanged.Subscribe(value => connected = value);

            Assert.True(connected);
        }

        [Fact]
        public void ThereIsNothingToShake()
        {
            Assert.False(_device.SupportsRumble);
            _device.Rumble(1, TimeSpan.FromSeconds(1));
        }
    }

    // A pad and the window drive one interface between them rather than two interfaces fighting over
    // the same cameras, so both sets of hands arrive on the same streams.
    public class WrappingAPad
    {
        private readonly FakeGamepadDevice _pad = new();

        [Fact]
        public void EitherSetOfHandsMovesTheCamera()
        {
            var device = new ControlSurfaceDevice(1, _pad);
            var seen = new List<StickPosition>();
            using var subscription = device.LeftStick.Subscribe(seen.Add);

            device.Move(1, 0);
            _pad.MoveLeftStick(-1, 0);

            Assert.Equal([new StickPosition(1, 0), new StickPosition(-1, 0)], seen);
        }

        [Fact]
        public void ItIsStillThePadThatDescribesTheInterface()
        {
            var device = new ControlSurfaceDevice(1, _pad);

            Assert.Equal(_pad.Description, device.Description);
        }

        [Fact]
        public void RumbleReachesThePad()
        {
            var device = new ControlSurfaceDevice(1, _pad);

            device.Rumble(0.5, TimeSpan.FromMilliseconds(120));

            Assert.Equal(0.5, Assert.Single(_pad.Rumbles).Intensity);
        }

        [Fact]
        public async Task DisposingTheSurfaceReleasesThePad()
        {
            var device = new ControlSurfaceDevice(1, _pad);

            await device.DisposeAsync();

            Assert.True(_pad.Disposed);
        }
    }
}
