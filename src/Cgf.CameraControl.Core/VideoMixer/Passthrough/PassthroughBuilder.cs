using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Core.VideoMixer.Passthrough;

public sealed class PassthroughBuilder(ILogger logger) : IBuilder<IVideoMixer>
{
    public IReadOnlyCollection<string> SupportedTypes => ["passthrough/default"];

    public Task<IVideoMixer> BuildAsync(ConfigEntry entry, CancellationToken cancellationToken)
    {
        var config = entry.Deserialize(PassthroughConfigurationContext.Default.PassthroughConfiguration);
        return Task.FromResult<IVideoMixer>(new Passthrough(logger, config.TallyMode));
    }
}
