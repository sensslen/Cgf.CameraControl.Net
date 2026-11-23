using Cgf.CameraControl.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Cgf.CameraControl.Core.VideoMixer.Passthrough;

public sealed record PassthroughConfiguration() : VideoMixerConfiguration(TypeName)
{
    public const string TypeName = "passthrough/default";

    public MixerTallyMode? TallyMode { get; set; }

    public override IEnumerable<ValueProblem> Validate() => ValueProblem.Of(new PassthroughConfigurationValidator(), this);
}

[OptionsValidator]
internal sealed partial class PassthroughConfigurationValidator : IValidateOptions<PassthroughConfiguration>;
