using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;

public sealed class SignalrPtzLancCameraBuilder(ILogger logger) : IBuilder<ICameraConnection>
{
    public IReadOnlyCollection<string> SupportedTypes => ["Signalr.PtzLanc"];

    public Task<ICameraConnection> BuildAsync(ConfigEntry entry, CancellationToken cancellationToken)
    {
        var config = entry.Deserialize(SignalrPtzLancCameraConfigurationContext.Default.SignalrPtzLancCameraConfiguration);
        var transport = new SignalrStateTransport(config.ConnectionUrl, config.ConnectionPort, logger);
        return Task.FromResult<ICameraConnection>(new SignalrPtzLancCamera(config, transport, logger));
    }
}
