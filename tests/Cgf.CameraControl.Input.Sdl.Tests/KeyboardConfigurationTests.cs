using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.Input.Sdl.Tests;

/// A keyboard interface binds every key it answers to, so the file is the only place a shortcut
/// exists and the panel on screen is drawn from it.
public class KeyboardConfigurationTests
{
    private const string Shipped =
        """
        {
          "type": "keyboard",
          "instance": 2,
          "videoMixer": 1,
          "connectionChange": { "type": "direct", "default": { "up": 1, "right": 2 } },
          "functions": {
            "wide": { "type": "key", "index": 1 },
            "tight": { "type": "macroLoop", "indexes": [1, 2] }
          },
          "keys": {
            "pan": { "left": "A", "right": "D" },
            "tilt": { "up": "W", "down": "S" },
            "zoom": { "in": "I", "out": "K" },
            "focus": { "far": "L", "near": "J" },
            "connectionChange": { "up": "Up", "right": "Right" },
            "input": { "D1": 1, "D2": 2, "D9": 9 },
            "function": { "F1": "wide", "F5": "tight" },
            "cut": "Enter",
            "auto": "Space"
          },
          "cameraMap": { "1": 1 }
        }
        """;

    [Fact]
    public void ReadsEveryAxisAsAPairOfKeys()
    {
        var keys = Read(Shipped).Keys;

        Assert.Equal("A", keys.Pan!.Left);
        Assert.Equal("S", keys.Tilt!.Down);
        Assert.Equal("I", keys.Zoom!.In);
        Assert.Equal("J", keys.Focus!.Near);
    }

    // The room a pad does not have: nine inputs where a direction pad offers four.
    [Fact]
    public void ReadsAKeyPerInput()
    {
        Assert.Equal(9, Read(Shipped).Keys.Input!["D9"]);
    }

    [Fact]
    public void BindsKeysToFunctionsByName()
    {
        var keys = Read(Shipped).Keys;

        Assert.Equal("wide", keys.Function!["F1"]);
        Assert.Equal("tight", keys.Function["F5"]);
    }

    [Fact]
    public void ReadsTheTransitionKeys()
    {
        var keys = Read(Shipped).Keys;

        Assert.Equal("Enter", keys.Cut);
        Assert.Equal("Space", keys.Auto);
    }

    [Fact]
    public void AnAxisNobodyBoundStaysUnbound()
    {
        Assert.Null(Read(Shipped.Replace("""
            "zoom": { "in": "I", "out": "K" },
            """, string.Empty, StringComparison.Ordinal)).Keys.Zoom);
    }

    // A deadzone belongs to a stick, and a keyboard has none, so a file that sets one has a
    // misunderstanding in it worth reporting rather than dropping.
    [Fact]
    public void ASectionOnlyAPadHasIsRejected()
    {
        var failure = Assert.Throws<ConfigValidationException>(() =>
            Read(Shipped.Replace("\"videoMixer\": 1,", "\"videoMixer\": 1,\n  \"deadzone\": 0.1,", StringComparison.Ordinal)));

        Assert.Contains("deadzone", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    // A name is a string, so nothing in the schema stops a binding naming a function that is not
    // there. Caught at load it points at the line; caught at run time it is a key that silently does
    // nothing in the middle of a service.
    [Fact]
    public void ABindingNamingAFunctionThatIsNotThereIsRejected()
    {
        var config = Read(Shipped.Replace("\"F5\": \"tight\"", "\"F5\": \"tigth\"", StringComparison.Ordinal));

        var failure = Assert.Throws<ConfigValidationException>(
            () => InterfaceValidation.Bindings(Entry(Shipped), config));

        Assert.Contains("tigth", failure.Message, StringComparison.Ordinal);
        Assert.Contains("tight", failure.Message, StringComparison.Ordinal);
    }

    private static ConfigEntry Entry(string json) =>
        Assert.Single(ConfigLoader.Load($$"""{ "interfaces": [ {{json}} ] }""", out _).Interfaces);

    private static KeyboardConfiguration Read(string json)
    {
        var config = ConfigLoader.Load($$"""{ "interfaces": [ {{json}} ] }""", out _);
        return Assert.Single(config.Interfaces)
            .Deserialize(InterfaceConfigurationContext.Default.KeyboardConfiguration);
    }
}
