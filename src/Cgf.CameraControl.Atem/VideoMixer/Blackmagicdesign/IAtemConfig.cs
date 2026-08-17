using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration.Converters;

namespace Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;

/// atemConfigurationSchema: configSchema.extend({ ip: z.string(), mixEffectBlock: z.int().nonnegative() })
public sealed record AtemConfiguration
{
    [JsonConverter(typeof(HostConverter))]
    public required string Ip { get; init; }

    [JsonConverter(typeof(NonNegativeIntConverter))]
    public required int MixEffectBlock { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AtemConfiguration))]
public sealed partial class AtemConfigurationContext : JsonSerializerContext;
