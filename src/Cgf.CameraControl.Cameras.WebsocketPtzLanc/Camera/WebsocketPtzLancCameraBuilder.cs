using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;

public sealed class WebsocketPtzLancCameraBuilder(ILogger logger) : IBuilder<ICameraConnection, CameraConfiguration>
{
    public IReadOnlyCollection<string> SupportedTypes => [WebsocketPtzLancCameraConfiguration.TypeName];

    public Task<ICameraConnection> BuildAsync(CameraConfiguration entry, CancellationToken cancellationToken)
    {
        var config = (WebsocketPtzLancCameraConfiguration)entry;
        var transport = new WebsocketTransport(new Uri($"ws://{config.Ip}/ws"), logger);
        return Task.FromResult<ICameraConnection>(new WebsocketPtzLancCamera(config, transport));
    }
}
