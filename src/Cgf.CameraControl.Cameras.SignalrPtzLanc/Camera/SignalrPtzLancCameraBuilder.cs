using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;

public sealed class SignalrPtzLancCameraBuilder(ILogger logger) : IBuilder<ICameraConnection, CameraConfiguration>
{
    public IReadOnlyCollection<string> SupportedTypes => [SignalrPtzLancCameraConfiguration.TypeName];

    public Task<ICameraConnection> BuildAsync(CameraConfiguration entry, CancellationToken cancellationToken)
    {
        var config = (SignalrPtzLancCameraConfiguration)entry;
        Validate.HttpUrl(config.ConnectionUrl, $"{entry}.connectionUrl");
        var transport = new SignalrStateTransport(config.ConnectionUrl, config.ConnectionPort, logger);
        return Task.FromResult<ICameraConnection>(new SignalrPtzLancCamera(config, transport, logger));
    }
}
