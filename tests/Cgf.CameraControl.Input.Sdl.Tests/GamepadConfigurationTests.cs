using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

namespace Cgf.CameraControl.Input.Sdl.Tests;

public class GamepadConfigurationTests
{
    // The shape ensi.json actually ships, addresses changed.
    private const string Shipped =
        """
        {
          "type": "logitech/F310",
          "serialNumber": "83234F94",
          "instance": 1,
          "videoMixer": 1,
          "enableChangingProgram": false,
          "connectionChange": {
            "type": "direct",
            "default": { "up": 1, "right": 2, "down": 3, "left": 4 },
            "alt": { "up": 5, "right": 7, "down": 10, "left": 16 }
          },
          "specialFunction": {
            "default": {
              "down": {
                "type": "macroToggle",
                "indexOn": 23,
                "indexOff": 24,
                "condition": { "type": "key", "key": 0 }
              }
            }
          },
          "cameraMap": { "1": 1, "2": 2, "3": 3, "4": 4, "7": 6 }
        }
        """;

    [Fact]
    public void ReadsTheShippedInterface()
    {
        var config = Read(Shipped);

        Assert.Equal(1, config.VideoMixer);
        Assert.False(config.EnableChangingProgram);
        Assert.Equal("83234F94", config.SerialNumber);
        Assert.Equal(6, config.CameraMap[7]);
    }

    [Fact]
    public void ReadsADirectConnectionChangeIncludingItsModifierSet()
    {
        var change = Assert.IsType<DirectConnectionChangeConfiguration>(Read(Shipped).ConnectionChange);

        Assert.Equal(2, change.Default[ButtonDirection.Right]);
        Assert.Equal(16, change.Alt![ButtonDirection.Left]);
        Assert.Null(change.AltLower);
    }

    [Fact]
    public void ReadsAMacroToggleWithItsCondition()
    {
        var special = Read(Shipped).SpecialFunction.Default[ButtonDirection.Down];

        var toggle = Assert.IsType<MacroToggleSpecialFunctionConfiguration>(special);
        Assert.Equal(23, toggle.IndexOn);
        Assert.Equal(24, toggle.IndexOff);
        Assert.Equal(0, Assert.IsType<KeyConditionConfiguration>(toggle.Condition).Key);
    }

    [Fact]
    public void ReadsAnAuxSelectionCondition()
    {
        var special = Read(WithSpecialFunction(
            """{ "type": "macroToggle", "indexOn": 20, "indexOff": 21, "condition": { "type": "aux_selection", "aux": 5, "selection": 16 } }"""));

        var toggle = Assert.IsType<MacroToggleSpecialFunctionConfiguration>(special.SpecialFunction.Default[ButtonDirection.Up]);
        var condition = Assert.IsType<AuxSelectionConditionConfiguration>(toggle.Condition);
        Assert.Equal(5, condition.Aux);
        Assert.Equal(16, condition.Selection);
    }

    [Theory]
    [InlineData("""{ "type": "key", "index": 1 }""", typeof(KeySpecialFunctionConfiguration))]
    [InlineData("""{ "type": "connectionChange", "index": 3 }""", typeof(ConnectionChangeSpecialFunctionConfiguration))]
    [InlineData("""{ "type": "macroLoop", "indexes": [1, 2, 3] }""", typeof(MacroLoopSpecialFunctionConfiguration))]
    public void ReadsEverySpecialFunctionType(string json, Type expected)
    {
        Assert.IsType(expected, Read(WithSpecialFunction(json)).SpecialFunction.Default[ButtonDirection.Up]);
    }

    [Fact]
    public void ReadsADirectionalConnectionChange()
    {
        var config = Read(WithConnectionChange(
            """{ "type": "directional", "directions": { "1": { "right": 2, "down": 3 }, "2": { "left": 1 } } }"""));

        var change = Assert.IsType<DirectionalConnectionChangeConfiguration>(config.ConnectionChange);
        Assert.Equal(3, change.Directions[1][ButtonDirection.Down]);
        Assert.Equal(1, change.Directions[2][ButtonDirection.Left]);
    }

    public class Defaults : GamepadConfigurationTests
    {
        [Fact]
        public void EnableChangingProgramDefaultsOn()
        {
            Assert.True(Read(Minimal()).EnableChangingProgram);
        }

        [Fact]
        public void AnAbsentSerialNumberMeansAutoDetect()
        {
            Assert.Null(Read(Minimal()).SerialNumber);
        }

        [Fact]
        public void DeadzoneAndRumbleHaveWorkingDefaults()
        {
            var config = Read(Minimal());

            Assert.Equal(0.05, config.Deadzone);
            Assert.True(config.Rumble);
        }
    }

    public class Refinements : GamepadConfigurationTests
    {
        [Fact]
        public void VideoMixerMustBePositive()
        {
            var error = Assert.Throws<ConfigValidationException>(() => Read(Minimal().Replace("\"videoMixer\": 1", "\"videoMixer\": 0")));

            Assert.Contains("positive", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AKeyIndexMustBePositive()
        {
            Assert.Throws<ConfigValidationException>(() => Read(WithSpecialFunction("""{ "type": "key", "index": 0 }""")));
        }

        [Fact]
        public void AnUnknownSpecialFunctionTypeIsRejected()
        {
            var error = Assert.Throws<ConfigValidationException>(() => Read(WithSpecialFunction("""{ "type": "explode" }""")));

            Assert.Contains("explode", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnUnknownButtonIsRejected()
        {
            Assert.Throws<ConfigValidationException>(() =>
                Read(Minimal().Replace("\"up\": 1", "\"sideways\": 1")));
        }
    }

    private static string Minimal() =>
        """
        {
          "type": "gamepad",
          "instance": 1,
          "videoMixer": 1,
          "connectionChange": { "type": "direct", "default": { "up": 1 } },
          "specialFunction": { "default": {} },
          "cameraMap": { "1": 1 }
        }
        """;

    private static string WithSpecialFunction(string json) =>
        Minimal().Replace("\"default\": {}", $"\"default\": {{ \"up\": {json} }}");

    private static string WithConnectionChange(string json) =>
        Minimal().Replace("""{ "type": "direct", "default": { "up": 1 } }""", json);

    private static GamepadConfiguration Read(string json)
    {
        var config = ConfigLoader.Load($$"""{ "interfaces": [ {{json}} ] }""", out _);
        return Assert.Single(config.Interfaces).Deserialize(GamepadConfigurationContext.Default.GamepadConfiguration);
    }
}
