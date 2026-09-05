using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Cameras.ViscaOverIp.Camera;

public sealed class ViscaOverIpCameraBuilder(ILogger logger) : IBuilder<ICameraConnection>
{
    public IReadOnlyCollection<string> SupportedTypes => ["viscaoverip"];

    public Task<ICameraConnection> BuildAsync(ConfigEntry entry, CancellationToken cancellationToken)
    {
        var config = entry.Deserialize(ViscaOverIpCameraConfigurationContext.Default.ViscaOverIpCameraConfiguration);
        var transport = new ViscaUdpTransport(config.Ip, config.Port, logger);
        return Task.FromResult<ICameraConnection>(new ViscaOverIpCamera(config, transport, logger));
    }
}
