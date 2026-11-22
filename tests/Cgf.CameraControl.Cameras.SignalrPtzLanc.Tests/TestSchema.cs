using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;

namespace Cgf.CameraControl.Cameras.SignalrPtzLanc.Tests;

/// The plugin's own records, registered the way the application registers them, so these tests read
/// entries through the same loader without naming every other plugin.
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(CameraConfiguration))]
[JsonSerializable(typeof(VideoMixerConfiguration))]
[JsonSerializable(typeof(InterfaceConfiguration))]
[JsonSerializable(typeof(SignalrPtzLancCameraConfiguration))]
internal sealed partial class TestConfigurationContext : JsonSerializerContext;

internal static class TestSchema
{
    public static ConfigurationSchema Schema { get; } = new ConfigurationSchema(TestConfigurationContext.Default)
        .Camera<SignalrPtzLancCameraConfiguration>(SignalrPtzLancCameraConfiguration.TypeName);

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
