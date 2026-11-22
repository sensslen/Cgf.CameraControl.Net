using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Configuration.Converters;
using Cgf.CameraControl.Core.VideoMixer.Passthrough;

namespace Cgf.CameraControl.Core.Tests;

/// Core names no plugin record, so its tests register records of their own, one per section, the way
/// the application registers the plugins'.
public sealed record LensCamera() : CameraConfiguration(TypeName)
{
    public const string TypeName = "Lens.Camera";

    [JsonRequired]
    [JsonConverter(typeof(HostConverter))]
    public string Ip { get; set; } = string.Empty;

    [JsonConverter(typeof(PortConverter))]
    public int Port { get; set; } = 52381;

    public bool ShowTallyLight { get; set; } = true;
}

public sealed record Pad() : InterfaceConfiguration(TypeName)
{
    public const string TypeName = "pad";

    public int VideoMixer { get; set; } = 1;

    public IReadOnlyDictionary<int, int> CameraMap { get; set; } = new Dictionary<int, int>();
}

[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(CameraConfiguration))]
[JsonSerializable(typeof(VideoMixerConfiguration))]
[JsonSerializable(typeof(InterfaceConfiguration))]
[JsonSerializable(typeof(LensCamera))]
[JsonSerializable(typeof(PassthroughConfiguration))]
[JsonSerializable(typeof(Pad))]
public sealed partial class TestConfigurationContext : JsonSerializerContext;

public static class TestSchema
{
    public static ConfigurationSchema Schema { get; } = new ConfigurationSchema(TestConfigurationContext.Default)
        .Camera<LensCamera>(LensCamera.TypeName)
        .VideoMixer<PassthroughConfiguration>(PassthroughConfiguration.TypeName)
        .Interface<Pad>(Pad.TypeName);

    public static RootConfig Load(string json, out IReadOnlyList<string> issues) =>
        ConfigLoader.Load(json, Schema, out issues);

    public static string Write(RootConfig config) => ConfigWriter.Write(config, Schema);
}
