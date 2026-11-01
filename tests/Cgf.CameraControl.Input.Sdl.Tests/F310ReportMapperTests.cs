using System.Reactive.Linq;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Hid;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.Input.Sdl.Tests;

public class F310ReportMapperTests
{
    [Fact]
    public void MapsAxisEndpointsAndCentreWithoutDrift()
    {
        using var mapper = new GamepadInputMapper(0);
        var states = new List<GamepadState>();
        using var subscription = mapper.State.Subscribe(states.Add);
        var reports = new F310ReportMapper(mapper);
        reports.Apply([0, 0, 255, 255, 0, 8, 0, 0, 0]);
        Assert.Equal(new StickPosition(-1, -1), states[^1].LeftStick);
        Assert.Equal(new StickPosition(-1, 1), states[^1].RightStick);
        reports.Apply([0, 128, 128, 128, 128, 8, 0, 0, 0]);
        Assert.Equal(default, states[^1].LeftStick);
        Assert.Equal(default, states[^1].RightStick);
        reports.Apply([0, 127, 127, 127, 127, 8, 0, 0, 0]);
        Assert.Equal(default, states[^1].LeftStick);
        Assert.Equal(default, states[^1].RightStick);
    }

    // Raw pin 4/5 values from sensslen/node-gamepad's F310 definition, with
    // HidSharp's extra report-ID byte. These fixtures do not use our bit packer.
    [Theory]
    [InlineData(0x18, 0x00, GamepadButtons.FaceLeft)]
    [InlineData(0x28, 0x00, GamepadButtons.FaceDown)]
    [InlineData(0x48, 0x00, GamepadButtons.FaceRight)]
    [InlineData(0x88, 0x00, GamepadButtons.FaceUp)]
    [InlineData(0x08, 0x01, GamepadButtons.LeftShoulder)]
    [InlineData(0x08, 0x02, GamepadButtons.RightShoulder)]
    [InlineData(0x08, 0x04, GamepadButtons.LeftTrigger)]
    [InlineData(0x08, 0x08, GamepadButtons.RightTrigger)]
    [InlineData(0x07, 0x00, GamepadButtons.DPadLeft | GamepadButtons.DPadUp)]
    public void MatchesTheTypescriptF310PinDefinitions(byte pin4, byte pin5, GamepadButtons expected)
    {
        using var mapper = new GamepadInputMapper(0);
        GamepadState state = default;
        using var subscription = mapper.State.Subscribe(value => state = value);
        new F310ReportMapper(mapper).Apply([0, 128, 128, 128, 128, pin4, pin5, 0, 0]);
        Assert.Equal(expected, state.Pressed);
    }

    [Theory]
    [InlineData(1, ButtonDirection.Left)]
    [InlineData(2, ButtonDirection.Down)]
    [InlineData(4, ButtonDirection.Right)]
    [InlineData(8, ButtonDirection.Up)]
    public void FaceButtonsFireOncePerPress(int buttons, ButtonDirection expected)
    {
        using var mapper = new GamepadInputMapper(0);
        var presses = new List<ButtonDirection>();
        using var subscription = mapper.SpecialFunctionRequested.Subscribe(presses.Add);
        var reports = new F310ReportMapper(mapper);
        var report = Report(buttons);
        reports.Apply(report);
        reports.Apply(report);
        Assert.Equal([expected], presses);
        reports.Apply(Report(0));
        reports.Apply(report);
        Assert.Equal([expected, expected], presses);
    }

    [Fact]
    public void HatDiagonalsReleaseCorrectlyAndDoNotRepeat()
    {
        using var mapper = new GamepadInputMapper(0);
        var presses = new List<ButtonDirection>();
        var states = new List<GamepadState>();
        using var subscription = mapper.ConnectionChangeRequested.Subscribe(presses.Add);
        using var stateSubscription = mapper.State.Subscribe(states.Add);
        var reports = new F310ReportMapper(mapper);
        reports.Apply(Report(0, 1));
        reports.Apply(Report(0, 1));
        Assert.Equal([ButtonDirection.Up, ButtonDirection.Right], presses);
        reports.Apply(Report(0, 8));
        Assert.Equal(GamepadButtons.None, states[^1].Pressed);
    }

    [Fact]
    public void ShouldersTriggersAndResetMatchTheSdlControls()
    {
        using var mapper = new GamepadInputMapper(0);
        var transitions = new List<MixerTransition>();
        var modifiers = new List<AltKeyConfiguration>();
        var states = new List<GamepadState>();
        using var transition = mapper.TransitionRequested.Subscribe(transitions.Add);
        using var modifier = mapper.Modifiers.Subscribe(modifiers.Add);
        using var state = mapper.State.Subscribe(states.Add);
        var reports = new F310ReportMapper(mapper);
        reports.Apply(Report(0xf0));
        reports.Apply(Report(0xf0));
        Assert.Equal([MixerTransition.Cut, MixerTransition.Auto], transitions);
        Assert.Equal(new AltKeyConfiguration(true, true), modifiers[^1]);
        Assert.Equal(1, states[^1].RightTrigger);
        reports.Reset();
        Assert.Equal(default, states[^1]);
        Assert.Equal(AltKeyConfiguration.None, modifiers[^1]);
        reports.Apply(Report(0x80));
        Assert.Equal(3, transitions.Count);
    }

    [Theory]
    [InlineData(8, 0)]
    [InlineData(9, 1)]
    [InlineData(10, 0)]
    public void RejectsUnknownReportShapes(int size, byte reportId)
    {
        using var mapper = new GamepadInputMapper(0);
        var report = new byte[size];
        report[0] = reportId;
        Assert.Throws<InvalidDataException>(() => new F310ReportMapper(mapper).Apply(report));
    }

    private static byte[] Report(int buttons, int hat = 8) =>
        [0, 128, 128, 128, 128, (byte)((buttons << 4) | hat), (byte)(buttons >> 4), 0, 0];
}
