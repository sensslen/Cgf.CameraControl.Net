using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.Input.Sdl.Tests;

/// One configuration file in production predates the current schema: it omits connectionChange.type
/// and specialFunction, and the TypeScript build rejects it too. It stays rejected here, so what
/// matters is that the operator is told which entry and which property to fix.
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
              "specialFunction": { "default": {} },
              "cameraMap": { "1": 1 }
            }
            """));

        Assert.Equal("logitech/F710[4].connectionChange", failure.Path);
        Assert.DoesNotContain("Path:", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingSpecialFunctionSetNamesTheEntry()
    {
        var failure = Assert.Throws<ConfigValidationException>(() => Read(
            """
            {
              "type": "logitech/F710",
              "instance": 4,
              "videoMixer": 1,
              "connectionChange": { "type": "direct", "default": { "right": 2 } },
              "cameraMap": { "1": 1 }
            }
            """));

        Assert.Contains("logitech/F710[4]", failure.Path, StringComparison.Ordinal);
        Assert.Contains("specialFunction", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static GamepadConfiguration Read(string json)
    {
        var config = ConfigLoader.Load($$"""{ "interfaces": [ {{json}} ] }""", out _);
        return Assert.Single(config.Interfaces).Deserialize(GamepadConfigurationContext.Default.GamepadConfiguration);
    }
}
