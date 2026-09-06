using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

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

    private static GamepadConfiguration Read(string json)
    {
        var config = ConfigLoader.Load($$"""{ "interfaces": [ {{json}} ] }""", out _);
        return Assert.Single(config.Interfaces).Deserialize(InterfaceConfigurationContext.Default.GamepadConfiguration);
    }
}
