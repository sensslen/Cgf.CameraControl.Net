using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

namespace Cgf.CameraControl.Input.Sdl.Tests;

/// A configuration file is written by hand, so what matters when one is wrong is that the operator is
/// told which entry and which property to fix rather than being handed a serializer's own words.
public class GamepadConfigurationRejectionTests
{
    [Fact]
    public void AConnectionChangeWithNoTypeNamesTheEntryAndTheProperty()
    {
        var failure = Assert.Throws<ConfigValidationException>(() => Read(
            """
            {
              "type": "logitech/F710",
              "instance": 4,
              "videoMixer": 1,
              "connectionChange": { "default": { "right": 2 } },
              "cameraMap": { "1": 1 }
            }
            """));

        Assert.Equal("logitech/F710[4].connectionChange", failure.Path);
        Assert.DoesNotContain("Path:", failure.Message, StringComparison.Ordinal);
    }

    // A pad cannot be driven from the window, so a keys block on one is a set of bindings that will
    // never fire. Saying so is the whole reason the two kinds have separate schemas.
    [Fact]
    public void ASectionTheInterfaceKindCannotUseIsRejected()
    {
        var failure = Assert.Throws<ConfigValidationException>(() => Read(
            """
            {
              "type": "gamepad",
              "instance": 4,
              "videoMixer": 1,
              "connectionChange": { "type": "direct", "default": { "right": 2 } },
              "keys": { "cut": "Enter" },
              "cameraMap": { "1": 1 }
            }
            """));

        Assert.Contains("gamepad[4]", failure.Path, StringComparison.Ordinal);
        Assert.Contains("keys", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheTypeOfAFunctionMayBeWrittenLast()
    {
        var config = Read(
            """
            {
              "instance": 4,
              "videoMixer": 1,
              "connectionChange": { "default": { "right": 2 }, "type": "direct" },
              "cameraMap": { "1": 1 },
              "functions": { "wide": { "index": 1, "type": "key" } },
              "type": "gamepad"
            }
            """);

        Assert.IsType<KeySpecialFunctionConfiguration>(config.Functions["wide"]);
    }

    [Fact]
    public void AMisspeltKeyInsideABindingIsRejected()
    {
        var failure = Assert.Throws<ConfigValidationException>(() => Read(
            """
            {
              "type": "gamepad",
              "instance": 4,
              "videoMixer": 1,
              "connectionChange": { "type": "direct", "default": { "right": 2 } },
              "cameraMap": { "1": 1 },
              "pad": { "altlower": {} }
            }
            """));

        Assert.Contains("altlower", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANullPadNamesTheProperty()
    {
        var failure = Assert.Throws<ConfigValidationException>(() => Read(
            """
            {
              "type": "gamepad",
              "instance": 4,
              "videoMixer": 1,
              "connectionChange": { "type": "direct", "default": { "right": 2 } },
              "cameraMap": { "1": 1 },
              "pad": null
            }
            """));

        Assert.Equal("gamepad[4].pad", failure.Path);
    }

    [Fact]
    public void AnEmptyMacroLoopIsRejected()
    {
        var entry = Entry(
            """
            {
              "type": "gamepad",
              "instance": 4,
              "videoMixer": 1,
              "connectionChange": { "type": "direct", "default": { "right": 2 } },
              "cameraMap": { "1": 1 },
              "functions": { "loop": { "type": "macroLoop", "indexes": [] } }
            }
            """);

        var failure = Assert.Throws<ConfigValidationException>(() =>
            InterfaceValidation.Bindings(entry, entry.Deserialize(InterfaceConfigurationContext.Default.GamepadConfiguration)));

        Assert.Equal("gamepad[4].functions.loop.indexes", failure.Path);
    }

    [Fact]
    public void ANegativeCameraMapEntryIsRejected()
    {
        var entry = Entry(
            """
            {
              "type": "gamepad",
              "instance": 4,
              "videoMixer": 1,
              "connectionChange": { "type": "direct", "default": { "right": 2 } },
              "cameraMap": { "1": -1 }
            }
            """);

        var failure = Assert.Throws<ConfigValidationException>(() =>
            InterfaceValidation.Bindings(entry, entry.Deserialize(InterfaceConfigurationContext.Default.GamepadConfiguration)));

        Assert.StartsWith("gamepad[4].cameraMap", failure.Path, StringComparison.Ordinal);
    }

    private static ConfigEntry Entry(string json) =>
        Assert.Single(ConfigLoader.Load($$"""{ "interfaces": [ {{json}} ] }""", out _).Interfaces);

    private static GamepadConfiguration Read(string json)
    {
        var config = ConfigLoader.Load($$"""{ "interfaces": [ {{json}} ] }""", out _);
        return Assert.Single(config.Interfaces).Deserialize(InterfaceConfigurationContext.Default.GamepadConfiguration);
    }
}
