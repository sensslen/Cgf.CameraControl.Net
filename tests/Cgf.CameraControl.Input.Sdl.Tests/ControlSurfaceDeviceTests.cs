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
            var ran = new List<string>();
            var inputs = new List<int>();
            var transitions = new List<MixerTransition>();
            using var one = _device.ConnectionChangeRequested.Subscribe(selected.Add);
            using var two = _device.FunctionRequested.Subscribe(ran.Add);
            using var three = _device.TransitionRequested.Subscribe(transitions.Add);
            using var four = _device.InputRequested.Subscribe(inputs.Add);

            _device.Select(ButtonDirection.Left);
            _device.Run("iso");
            _device.SelectInput(5);
            _device.Transition(MixerTransition.Auto);

            Assert.Equal(ButtonDirection.Left, Assert.Single(selected));
            Assert.Equal("iso", Assert.Single(ran));
            Assert.Equal(5, Assert.Single(inputs));
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

    // Nothing draws a keyboard, so the state it reports is the two pads on screen and nothing else.
    public class DrawnState
    {
        private readonly ControlSurfaceDevice _device = new(1);

        [Fact]
        public void TheSticksFollowTheMouse()
        {
            var seen = new List<GamepadState>();
            using var subscription = _device.State.Subscribe(seen.Add);

            _device.Move(1, -1);
            _device.Lens(0.5, 0);

            Assert.Equal(new StickPosition(1, -1), seen[^1].LeftStick);
            Assert.Equal(new StickPosition(0.5, 0), seen[^1].RightStick);
        }

        [Fact]
        public void NoButtonIsEverDrawnAsHeld()
        {
            var seen = new List<GamepadState>();
            using var subscription = _device.State.Subscribe(seen.Add);

            _device.Run("iso");
            _device.Transition(MixerTransition.Cut);

            Assert.All(seen, state => Assert.Equal(GamepadButtons.None, state.Pressed));
        }
    }
}
