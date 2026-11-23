using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Cameras.ViscaOverIp.Camera;

public sealed class ViscaOverIpCameraBuilder(ILogger logger) : IBuilder<ICameraConnection, CameraConfiguration>
{
    public Task<ICameraConnection>? BuildAsync(CameraConfiguration entry, CancellationToken cancellationToken) =>
        entry is ViscaOverIpCameraConfiguration config ? Task.FromResult(Build(config)) : null;

    private ICameraConnection Build(ViscaOverIpCameraConfiguration config)
    {
        var port = Validate.Port(config.Port ?? ViscaOverIpCameraConfiguration.DefaultPort, $"{config}.port");
        var transport = new ViscaUdpTransport(config.Ip, port, logger);
        return new ViscaOverIpCamera(config, transport, logger);
    }
}
