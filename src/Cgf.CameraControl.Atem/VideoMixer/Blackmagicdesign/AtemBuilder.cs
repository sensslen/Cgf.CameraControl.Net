using AtemSharp.DependencyInjection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Core.VideoMixer;

namespace Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;

public sealed class AtemBuilder(ILogger logger, IServices services) : IBuilder<IVideoMixer, VideoMixerConfiguration>
{
    private readonly AtemFactory _atemFactory = new(logger, services);

    public Task<IVideoMixer>? BuildAsync(VideoMixerConfiguration entry, CancellationToken cancellationToken) =>
        entry is AtemConfiguration config ? BuildAsync(config, cancellationToken) : null;

    private async Task<IVideoMixer> BuildAsync(AtemConfiguration config, CancellationToken cancellationToken)
    {
        var atem = new Atem(config, _atemFactory);
        try
        {
            await atem.StartupAsync(cancellationToken).ConfigureAwait(false);
            return atem;
        }
        catch
        {
            await atem.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
