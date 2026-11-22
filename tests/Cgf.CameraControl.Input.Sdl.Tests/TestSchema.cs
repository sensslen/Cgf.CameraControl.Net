using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.Input.Sdl.Tests;

/// The plugin's own records, registered the way the application registers them, so these tests read
/// entries through the same loader without naming every other plugin.
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(CameraConfiguration))]
[JsonSerializable(typeof(VideoMixerConfiguration))]
[JsonSerializable(typeof(InterfaceConfiguration))]
[JsonSerializable(typeof(GamepadConfiguration))]
[JsonSerializable(typeof(LogitechF310Configuration))]
[JsonSerializable(typeof(LogitechF710Configuration))]
[JsonSerializable(typeof(LogitechRumblepad2Configuration))]
[JsonSerializable(typeof(KeyboardConfiguration))]
internal sealed partial class TestConfigurationContext : JsonSerializerContext;

internal static class TestSchema
{
    public static ConfigurationSchema Schema { get; } = new ConfigurationSchema(TestConfigurationContext.Default)
        .Interface<GamepadConfiguration>(GamepadConfiguration.TypeName)
        .Interface<LogitechF310Configuration>(LogitechF310Configuration.TypeName)
        .Interface<LogitechF710Configuration>(LogitechF710Configuration.TypeName)
        .Interface<LogitechRumblepad2Configuration>(LogitechRumblepad2Configuration.TypeName)
        .Interface<KeyboardConfiguration>(KeyboardConfiguration.TypeName);

    public static RootConfig Load(string json) => ConfigLoader.Load(json, Schema, out _);

    /// The single entry of the section, read as the record its type names.
    public static T Read<T>(string section, string entry)
        where T : EntryConfiguration => Assert.IsType<T>(Entry(section, entry), exactMatch: false);

    /// Why the single entry of the section could not be read.
    public static string Rejected(string section, string entry) =>
        Assert.IsAssignableFrom<IInvalidConfiguration>(Entry(section, entry)).Problem;

    private static EntryConfiguration Entry(string section, string entry)
    {
        var config = Load($$"""{ "{{section}}": [ {{entry}} ] }""");
        return section switch
        {
            "cams" => Assert.Single(config.Cams),
            "videoMixers" => Assert.Single(config.VideoMixers),
            _ => Assert.Single(config.Interfaces),
        };
    }
}
