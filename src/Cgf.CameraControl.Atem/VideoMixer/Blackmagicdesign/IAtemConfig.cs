using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration;

namespace Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;

/// atemConfigurationSchema: configSchema.extend({ ip: z.string(), mixEffectBlock: z.int().nonnegative() })
public sealed record AtemConfiguration
{
    public required string Ip { get; init; }

    public required int MixEffectBlock { get; init; }

    public AtemConfiguration Validated(ConfigEntry entry)
    {
        Validate.NonNegative(MixEffectBlock, $"{entry}.mixEffectBlock");
        return this;
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AtemConfiguration))]
public sealed partial class AtemConfigurationContext : JsonSerializerContext;
