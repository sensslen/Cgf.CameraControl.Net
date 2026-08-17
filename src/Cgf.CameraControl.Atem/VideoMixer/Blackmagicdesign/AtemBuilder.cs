using AtemSharp.DependencyInjection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Core.VideoMixer;

namespace Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;

public sealed class AtemBuilder(ILogger logger, IServices services) : IBuilder<IVideoMixer>
{
    private readonly AtemFactory _atemFactory = new(logger, services);

    public IReadOnlyCollection<string> SupportedTypes => ["blackmagicdesign/atem"];

    public async Task<IVideoMixer> BuildAsync(ConfigEntry entry, CancellationToken cancellationToken)
    {
        var config = entry.Deserialize(AtemConfigurationContext.Default.AtemConfiguration);
        var atem = new Atem(config, _atemFactory);
        await atem.StartupAsync(cancellationToken).ConfigureAwait(false);
        return atem;
    }
}
