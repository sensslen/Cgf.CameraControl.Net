using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Cameras.ViscaOverIp.Camera;

public sealed class ViscaOverIpCameraBuilder(ILogger logger) : IBuilder<ICameraConnection, CameraConfiguration>
{
    public IReadOnlyCollection<string> SupportedTypes => [ViscaOverIpCameraConfiguration.TypeName];

    public Task<ICameraConnection> BuildAsync(CameraConfiguration entry, CancellationToken cancellationToken)
    {
        var config = (ViscaOverIpCameraConfiguration)entry;
        Validate.Port(config.Port, $"{entry}.port");
        var transport = new ViscaUdpTransport(config.Ip, config.Port, logger);
        return Task.FromResult<ICameraConnection>(new ViscaOverIpCamera(config, transport, logger));
    }
}
