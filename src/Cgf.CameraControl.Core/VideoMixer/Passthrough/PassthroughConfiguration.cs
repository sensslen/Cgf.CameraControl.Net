using Cgf.CameraControl.Core.Configuration;

namespace Cgf.CameraControl.Core.VideoMixer.Passthrough;

public sealed record PassthroughConfiguration() : VideoMixerConfiguration(TypeName)
{
    public const string TypeName = "passthrough/default";

    public MixerTallyMode TallyMode { get; set; } = MixerTallyMode.PreviewProgram;
}
