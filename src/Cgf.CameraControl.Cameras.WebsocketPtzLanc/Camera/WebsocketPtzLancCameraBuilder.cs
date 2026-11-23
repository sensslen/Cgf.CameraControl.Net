using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;

public sealed class WebsocketPtzLancCameraBuilder(ILogger logger) : IBuilder<ICameraConnection, CameraConfiguration>
{
    public Task<ICameraConnection>? BuildAsync(CameraConfiguration entry, CancellationToken cancellationToken) =>
        entry is WebsocketPtzLancCameraConfiguration config
            ? Task.FromResult<ICameraConnection>(
                new WebsocketPtzLancCamera(config, new WebsocketTransport(new Uri($"ws://{config.Ip}/ws"), logger)))
            : null;
}
