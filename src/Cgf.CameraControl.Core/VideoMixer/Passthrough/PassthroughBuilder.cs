using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Core.VideoMixer.Passthrough;

public sealed class PassthroughBuilder(ILogger logger) : IBuilder<IVideoMixer, VideoMixerConfiguration>
{
    public Task<IVideoMixer>? BuildAsync(VideoMixerConfiguration entry, CancellationToken cancellationToken) =>
        entry is PassthroughConfiguration config
            ? Task.FromResult<IVideoMixer>(new Passthrough(logger, config.TallyMode ?? MixerTallyMode.PreviewProgram))
            : null;
}
