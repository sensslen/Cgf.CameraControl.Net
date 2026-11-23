using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

namespace Cgf.CameraControl.App.Tests.Configuration;

/// A configuration file is written by hand, so what matters when one is wrong is that the operator is
/// told which entry and which property to fix rather than being handed a serializer's own words.
public class GamepadConfigurationRejectionTests
{
    [Fact]
    public void AConnectionChangeWithNoTypeNamesTheEntryAndTheProperty()
    {
        var failure = Rejected(
            """
            {
              "type": "logitech/F710",
              "instance": 4,
              "videoMixer": 1,
              "connectionChange": { "default": { "right": 2 } },
              "cameraMap": { "1": 1 }
            }
            """);

        Assert.Contains("logitech/F710[4].connectionChange: ", failure, StringComparison.Ordinal);
        Assert.DoesNotContain("Path:", failure, StringComparison.Ordinal);
    }

    // A pad cannot be driven from the window, so a keys block on one is a set of bindings that will
    // never fire. Saying so is the whole reason the two kinds have separate schemas.
    [Fact]
    public void ASectionTheInterfaceKindCannotUseIsRejected()
    {
        var failure = Rejected(
            """
            {
              "type": "gamepad",
              "instance": 4,
              "videoMixer": 1,
              "connectionChange": { "type": "direct", "default": { "right": 2 } },
              "keys": { "cut": "Enter" },
              "cameraMap": { "1": 1 }
            }
            """);

        Assert.Contains("gamepad[4]", failure, StringComparison.Ordinal);
        Assert.Contains("keys", failure, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AMisspeltKeyInsideABindingIsRejected()
    {
        var failure = Rejected(
            """
            {
              "type": "gamepad",
              "instance": 4,
              "videoMixer": 1,
              "connectionChange": { "type": "direct", "default": { "right": 2 } },
              "cameraMap": { "1": 1 },
              "pad": { "altlower": {} }
            }
            """);

        Assert.Contains("altlower", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyMacroLoopIsRejected()
    {
        var entry = Read(
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
            InterfaceValidation.Bindings(entry));

        Assert.Equal("gamepad[4].functions.loop.indexes", failure.Path);
    }

    [Fact]
    public void ANegativeCameraMapEntryIsRejected()
    {
        var entry = Read(
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
            InterfaceValidation.Bindings(entry));

        Assert.StartsWith("gamepad[4].cameraMap", failure.Path, StringComparison.Ordinal);
    }

    private static GamepadConfiguration Read(string json) => ConfigurationReader.Read<GamepadConfiguration>("interfaces", json);

    private static string Rejected(string json) => ConfigurationReader.Rejected("interfaces", json);
}
