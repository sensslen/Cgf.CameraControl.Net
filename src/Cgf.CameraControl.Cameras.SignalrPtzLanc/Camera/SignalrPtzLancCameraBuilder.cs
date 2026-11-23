using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;

public sealed class SignalrPtzLancCameraBuilder(ILogger logger) : IBuilder<ICameraConnection, CameraConfiguration>
{
    public Task<ICameraConnection>? BuildAsync(CameraConfiguration entry, CancellationToken cancellationToken) =>
        entry is SignalrPtzLancCameraConfiguration config ? Task.FromResult(Build(config)) : null;

    private ICameraConnection Build(SignalrPtzLancCameraConfiguration config)
    {
        Validate.HttpUrl(config.ConnectionUrl, $"{config}.connectionUrl");
        var transport = new SignalrStateTransport(config.ConnectionUrl, config.ConnectionPort, logger);
        return new SignalrPtzLancCamera(config, transport, logger);
    }
}
