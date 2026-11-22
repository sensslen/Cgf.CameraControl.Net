using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Configuration.Converters;
using Cgf.CameraControl.Core.VideoMixer;

namespace Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;

/// atemConfigurationSchema: configSchema.extend({ ip: z.string(), mixEffectBlock: z.int().nonnegative() })
/// On the `get; set;` and [JsonRequired] conventions, see EntryConfiguration.
public sealed record AtemConfiguration() : VideoMixerConfiguration(TypeName)
{
    public const string TypeName = "blackmagicdesign/atem";

    [JsonRequired]
    [JsonConverter(typeof(HostConverter))]
    public string Ip { get; set; } = string.Empty;

    [JsonRequired]
    [JsonConverter(typeof(NonNegativeIntConverter))]
    public int MixEffectBlock { get; set; }

    public MixerTallyMode TallyMode { get; set; } = MixerTallyMode.PreviewProgram;
}
