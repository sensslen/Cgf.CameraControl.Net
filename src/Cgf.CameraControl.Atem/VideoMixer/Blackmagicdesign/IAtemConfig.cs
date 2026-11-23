using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.VideoMixer;
using Microsoft.Extensions.Options;

namespace Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;

/// atemConfigurationSchema: configSchema.extend({ ip: z.string(), mixEffectBlock: z.int().nonnegative() })
/// On the `get; set;` and [JsonRequired] conventions, see EntryConfiguration.
public sealed record AtemConfiguration() : VideoMixerConfiguration(TypeName)
{
    public const string TypeName = "blackmagicdesign/atem";

    [JsonRequired]
    [Required]
    [Host]
    public string Ip { get; set; } = string.Empty;

    [JsonRequired]
    [Range(0, int.MaxValue)]
    public int MixEffectBlock { get; set; }

    public MixerTallyMode? TallyMode { get; set; }

    public override IEnumerable<ValueProblem> Validate() => ValueProblem.Of(new AtemConfigurationValidator(), this);
}

[OptionsValidator]
internal sealed partial class AtemConfigurationValidator : IValidateOptions<AtemConfiguration>;
