using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration.Converters;

namespace Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;

/// atemConfigurationSchema: configSchema.extend({ ip: z.string(), mixEffectBlock: z.int().nonnegative() })
/// On the `get; set;` and [JsonRequired] conventions, see WebsocketPtzLancCameraConfiguration.
public sealed record AtemConfiguration
{
    [JsonRequired]
    [JsonConverter(typeof(HostConverter))]
    public string Ip { get; set; } = null!;

    [JsonRequired]
    [JsonConverter(typeof(NonNegativeIntConverter))]
    public int MixEffectBlock { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AtemConfiguration))]
public sealed partial class AtemConfigurationContext : JsonSerializerContext;
