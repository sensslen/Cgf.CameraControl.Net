using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;

public sealed class WebsocketPtzLancCameraBuilder(ILogger logger) : IBuilder<ICameraConnection>
{
    public IReadOnlyCollection<string> SupportedTypes => ["Websocket.PtzLanc"];

    public Task<ICameraConnection> BuildAsync(ConfigEntry entry, CancellationToken cancellationToken)
    {
        var config = entry.Deserialize(WebsocketPtzLancCameraConfigurationContext.Default.WebsocketPtzLancCameraConfiguration);
        var transport = new WebsocketTransport(new Uri($"ws://{config.Ip}/ws"), logger);
        return Task.FromResult<ICameraConnection>(new WebsocketPtzLancCamera(config, transport));
    }
}
