using System.Reactive.Linq;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using SDL3;

namespace Cgf.CameraControl.Input.Sdl.Tests;

public class GamepadInputMapperTests
{
    private const short Max = 32767;

    private readonly GamepadInputMapper _mapper = new(0);

    public class Sticks : GamepadInputMapperTests
    {
        // The pad this replaces interpolated raw HID through a table that inverted three of the four
        // axes. SDL reports a signed range with Y positive downward, which is the same inversion on
        // everything except tilt.
        [Fact]
        public void PushingLeftPansLeft()
        {
            var positions = Record(_mapper.LeftStick);

            _mapper.Axis(SDL.GamepadAxis.LeftX, -Max);

            Assert.Equal(1, positions.Single().X);
        }

        [Fact]
        public void PullingBackTiltsUp()
        {
            var positions = Record(_mapper.LeftStick);

            _mapper.Axis(SDL.GamepadAxis.LeftY, -Max);

            Assert.Equal(-1, positions.Single().Y);
        }

        [Fact]
        public void PushingForwardZoomsIn()
        {
            var positions = Record(_mapper.RightStick);

            _mapper.Axis(SDL.GamepadAxis.RightY, -Max);

            Assert.Equal(1, positions.Single().Y);
        }

        [Fact]
        public void PushingRightFocusesNear()
        {
            var positions = Record(_mapper.RightStick);

            _mapper.Axis(SDL.GamepadAxis.RightX, Max);

            Assert.Equal(-1, positions.Single().X);
        }

        [Fact]
        public void TheOtherAxisOfTheSameStickIsPreserved()
        {
            var positions = Record(_mapper.LeftStick);

            _mapper.Axis(SDL.GamepadAxis.LeftX, -Max);
            _mapper.Axis(SDL.GamepadAxis.LeftY, Max);

            Assert.Equal(new StickPosition(1, 1), positions.Last());
        }

        // SDL repeats the current value on some drivers, and every emission is a websocket frame to
        // a camera.
        [Fact]
        public void RepeatingTheSameValueEmitsNothing()
        {
            var positions = Record(_mapper.LeftStick);

            _mapper.Axis(SDL.GamepadAxis.LeftX, Max);
            _mapper.Axis(SDL.GamepadAxis.LeftX, Max);

            Assert.Single(positions);
        }
    }

    public class Deadzone : GamepadInputMapperTests
    {
        private readonly GamepadInputMapper _gated = new(0.5);

        [Fact]
        public void TravelInsideTheDeadzoneIsIgnored()
        {
            var positions = Record(_gated.LeftStick);

            _gated.Axis(SDL.GamepadAxis.LeftX, (short)(Max / 4));

            Assert.Empty(positions);
        }

        // Without the rescale a stick leaving the deadzone would jump straight to the deadzone width
        // of camera speed instead of easing away from a standstill.
        [Fact]
        public void TravelOutsideTheDeadzoneIsRescaledOverTheFullRange()
        {
            var positions = Record(_gated.LeftStick);

            _gated.Axis(SDL.GamepadAxis.LeftX, (short)-(Max * 3 / 4));

            Assert.Equal(0.5, positions.Single().X, 3);
        }

        [Fact]
        public void FullTravelStillReachesFullSpeed()
        {
            var positions = Record(_gated.LeftStick);

            _gated.Axis(SDL.GamepadAxis.LeftX, -Max);

            Assert.Equal(1, positions.Single().X);
        }
    }

    public class Triggers : GamepadInputMapperTests
    {
        [Fact]
        public void TheRightTriggerRunsAnAutoTransition()
        {
            var transitions = Record(_mapper.TransitionRequested);

            _mapper.Axis(SDL.GamepadAxis.RightTrigger, Max);

            Assert.Equal(MixerTransition.Auto, transitions.Single());
        }

        [Fact]
        public void TheLeftTriggerIsTheLowerModifier()
        {
            var modifiers = Record(_mapper.Modifiers);

            _mapper.Axis(SDL.GamepadAxis.LeftTrigger, Max);
            _mapper.Axis(SDL.GamepadAxis.LeftTrigger, 0);

            Assert.Equal(
                [AltKeyConfiguration.None, new AltKeyConfiguration(Alt: false, AltLower: true), AltKeyConfiguration.None],
                modifiers);
        }

        // A finger resting on the edge of an analogue trigger crosses a plain threshold repeatedly,
        // which on the right trigger would fire a transition each time.
        [Fact]
        public void HoveringAtTheThresholdDoesNotRetrigger()
        {
            var transitions = Record(_mapper.TransitionRequested);

            _mapper.Axis(SDL.GamepadAxis.RightTrigger, (short)(Max * 0.65));
            _mapper.Axis(SDL.GamepadAxis.RightTrigger, (short)(Max * 0.55));
            _mapper.Axis(SDL.GamepadAxis.RightTrigger, (short)(Max * 0.65));

            Assert.Single(transitions);
        }

        [Fact]
        public void ReleasingPastTheLowerThresholdArmsItAgain()
        {
            var transitions = Record(_mapper.TransitionRequested);

            _mapper.Axis(SDL.GamepadAxis.RightTrigger, Max);
            _mapper.Axis(SDL.GamepadAxis.RightTrigger, 0);
            _mapper.Axis(SDL.GamepadAxis.RightTrigger, Max);

            Assert.Equal(2, transitions.Count);
        }

        // The drawing lights a trigger when the mapper has switched it, not when it has moved, so
        // the picture and the transition it stands for agree at the threshold.
        [Fact]
        public void TheDrawnTriggerSwitchesWhereTheTransitionDoes()
        {
            var states = Record(_mapper.State);

            _mapper.Axis(SDL.GamepadAxis.RightTrigger, (short)(Max * 0.55));
            _mapper.Axis(SDL.GamepadAxis.RightTrigger, (short)(Max * 0.65));
            _mapper.Axis(SDL.GamepadAxis.RightTrigger, (short)(Max * 0.55));

            Assert.Equal(
                [false, true, true],
                states.Skip(1).Select(state => state.IsPressed(GamepadButtons.RightTrigger)));
        }
    }

    public class Buttons : GamepadInputMapperTests
    {
        [Theory]
        [InlineData(SDL.GamepadButton.DPadUp, ButtonDirection.Up)]
        [InlineData(SDL.GamepadButton.DPadDown, ButtonDirection.Down)]
        [InlineData(SDL.GamepadButton.DPadLeft, ButtonDirection.Left)]
        [InlineData(SDL.GamepadButton.DPadRight, ButtonDirection.Right)]
        public void TheDirectionPadChangesTheConnection(SDL.GamepadButton button, ButtonDirection expected)
        {
            var directions = Record(_mapper.ConnectionChangeRequested);

            _mapper.Button(button, down: true);

            Assert.Equal(expected, directions.Single());
        }

        [Theory]
        [InlineData(SDL.GamepadButton.North, ButtonDirection.Up)]
        [InlineData(SDL.GamepadButton.South, ButtonDirection.Down)]
        [InlineData(SDL.GamepadButton.West, ButtonDirection.Left)]
        [InlineData(SDL.GamepadButton.East, ButtonDirection.Right)]
        public void AFaceButtonRunsTheSpecialFunctionForItsDirection(
            SDL.GamepadButton button,
            ButtonDirection expected)
        {
            var directions = Record(_mapper.SpecialFunctionRequested);

            _mapper.Button(button, down: true);

            Assert.Equal(expected, directions.Single());
        }

        [Fact]
        public void TheRightShoulderCuts()
        {
            var transitions = Record(_mapper.TransitionRequested);

            _mapper.Button(SDL.GamepadButton.RightShoulder, down: true);

            Assert.Equal(MixerTransition.Cut, transitions.Single());
        }

        [Fact]
        public void ReleasingAButtonDoesNothing()
        {
            var directions = Record(_mapper.ConnectionChangeRequested);

            _mapper.Button(SDL.GamepadButton.DPadUp, down: true);
            _mapper.Button(SDL.GamepadButton.DPadUp, down: false);

            Assert.Single(directions);
        }

        [Fact]
        public void TheLeftShoulderIsTheModifier()
        {
            var modifiers = Record(_mapper.Modifiers);

            _mapper.Button(SDL.GamepadButton.LeftShoulder, down: true);

            Assert.Equal(new AltKeyConfiguration(Alt: true, AltLower: false), modifiers.Last());
        }

        [Fact]
        public void BothModifiersCanBeHeldTogether()
        {
            var modifiers = Record(_mapper.Modifiers);

            _mapper.Button(SDL.GamepadButton.LeftShoulder, down: true);
            _mapper.Axis(SDL.GamepadAxis.LeftTrigger, Max);

            Assert.Equal(new AltKeyConfiguration(Alt: true, AltLower: true), modifiers.Last());
        }
    }

    public class Reset : GamepadInputMapperTests
    {
        [Fact]
        public void APadUnpluggedMidMoveRecentresItsSticks()
        {
            _mapper.Axis(SDL.GamepadAxis.LeftX, Max);
            _mapper.Axis(SDL.GamepadAxis.RightY, Max);
            var left = Record(_mapper.LeftStick);
            var right = Record(_mapper.RightStick);

            _mapper.Reset();

            Assert.Equal(default, left.Single());
            Assert.Equal(default, right.Single());
        }

        [Fact]
        public void AModifierHeldWhileUnpluggingIsCleared()
        {
            _mapper.Button(SDL.GamepadButton.LeftShoulder, down: true);
            var modifiers = Record(_mapper.Modifiers);

            _mapper.Reset();

            Assert.Equal(AltKeyConfiguration.None, modifiers.Last());
        }
    }

    private static List<T> Record<T>(IObservable<T> source)
    {
        List<T> recorded = [];
        source.Subscribe(recorded.Add);
        return recorded;
    }
}
