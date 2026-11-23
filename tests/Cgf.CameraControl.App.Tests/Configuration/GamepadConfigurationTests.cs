using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

namespace Cgf.CameraControl.App.Tests.Configuration;

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
          "functions": {
            "lower": {
              "type": "macroToggle",
              "indexOn": 23,
              "indexOff": 24,
              "condition": { "type": "key", "key": 0 }
            }
          },
          "pad": { "default": { "down": "lower" } },
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
    public void BackendIsUnsetUnlessHidIsOptedInto()
    {
        Assert.Null(Read(Shipped).Backend);
        Assert.Equal(GamepadBackend.Hid,
            Read(Shipped.Replace("\"serialNumber\":", "\"backend\": \"hid\", \"serialNumber\":")).Backend);
    }

    [Fact]
    public void UnknownBackendIsRejected()
    {
        Rejected(Shipped.Replace("\"serialNumber\":", "\"backend\": \"guessed\", \"serialNumber\":"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("----")]
    public void HidBackendRequiresAnIdentifyingSerial(string? serial)
    {
        var shape = serial is null
            ? Shipped.Replace("\"serialNumber\": \"83234F94\",", "\"backend\": \"hid\",", StringComparison.Ordinal)
            : Shipped.Replace("\"serialNumber\": \"83234F94\",", $"\"backend\": \"hid\", \"serialNumber\": \"{serial}\",", StringComparison.Ordinal);

        Assert.Contains("logitech/F310[1].serialNumber: ", Rejected(shape), StringComparison.Ordinal);
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
    public void APadBindsAFaceButtonToAFunctionByName()
    {
        Assert.Equal("lower", Read(Shipped).Pad!.Default![ButtonDirection.Down]);
    }

    [Fact]
    public void ReadsAMacroToggleWithItsCondition()
    {
        var special = Read(Shipped).Functions!["lower"];

        var toggle = Assert.IsType<MacroToggleSpecialFunctionConfiguration>(special);
        Assert.Equal(23, toggle.IndexOn);
        Assert.Equal(24, toggle.IndexOff);
        Assert.Equal(0, Assert.IsType<KeyConditionConfiguration>(toggle.Condition).Key);
    }

    [Fact]
    public void ReadsAnAuxSelectionCondition()
    {
        var special = Read(WithFunction(
            """{ "type": "macroToggle", "indexOn": 20, "indexOff": 21, "condition": { "type": "aux_selection", "aux": 5, "selection": 16 } }"""));

        var toggle = Assert.IsType<MacroToggleSpecialFunctionConfiguration>(special.Functions!["iso"]);
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
        Assert.IsType(expected, Read(WithFunction(json)).Functions!["iso"]);
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
        public void AbsentSettingsAreLeftUnset()
        {
            var config = Read(Minimal());

            Assert.Null(config.EnableChangingProgram);
            Assert.Null(config.SerialNumber);
            Assert.Null(config.Deadzone);
            Assert.Null(config.Rumble);
            Assert.Null(config.Pad);
            Assert.Null(config.Functions);
        }
    }

    public class Refinements : GamepadConfigurationTests
    {
        [Fact]
        public void VideoMixerMustBePositive()
        {
            var error = Rejected(Minimal().Replace("\"videoMixer\": 1", "\"videoMixer\": 0"));

            Assert.Contains("gamepad[1].videoMixer: ", error, StringComparison.Ordinal);
            Assert.Contains("must be between 1 and", error, StringComparison.Ordinal);
        }

        [Fact]
        public void AKeyIndexMustBePositive()
        {
            Rejected(WithFunction("""{ "type": "key", "index": 0 }"""));
        }

        [Fact]
        public void AnUnknownSpecialFunctionTypeIsRejected()
        {
            var error = Rejected(WithFunction("""{ "type": "explode" }"""));

            Assert.Contains("explode", error, StringComparison.Ordinal);
        }

        [Fact]
        public void AnUnknownButtonIsRejected()
        {
            Rejected(Minimal().Replace("\"up\": 1", "\"sideways\": 1"));
        }
    }

    private static string Minimal() =>
        """
        {
          "type": "gamepad",
          "instance": 1,
          "videoMixer": 1,
          "connectionChange": { "type": "direct", "default": { "up": 1 } },
          "cameraMap": { "1": 1 }
        }
        """;

    /// The function is named once and bound by that name, which is the shape every binding takes.
    private static string WithFunction(string json) =>
        Minimal().Replace(
            "\"cameraMap\"",
            $$"""
              "functions": { "iso": {{json}} },
              "pad": { "default": { "up": "iso" } },
              "cameraMap"
              """);

    private static string WithConnectionChange(string json) =>
        Minimal().Replace("""{ "type": "direct", "default": { "up": 1 } }""", json);

    private static GamepadConfiguration Read(string json) => ConfigurationReader.Read<GamepadConfiguration>("interfaces", json);

    private static string Rejected(string json) => ConfigurationReader.Rejected("interfaces", json);
}
