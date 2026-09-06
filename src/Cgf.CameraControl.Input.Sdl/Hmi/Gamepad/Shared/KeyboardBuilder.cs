using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Hmi;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Core.VideoMixer;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

/// An interface with no pad behind it, driven from the window alone. A desk that wants both points
/// one of these and one gamepad entry at the same mixer.
public sealed class KeyboardBuilder(
    VideoMixerFactory mixers,
    CameraConnectionFactory cameras,
    ICollection<ControlSurfaceDevice> surfaces,
    ILogger logger) : IBuilder<IHmi>
{
    public IReadOnlyCollection<string> SupportedTypes => ["keyboard"];

    public Task<IHmi> BuildAsync(ConfigEntry entry, CancellationToken cancellationToken)
    {
        var config = entry.Deserialize(InterfaceConfigurationContext.Default.KeyboardConfiguration);
        var mixer = mixers.Get(config.VideoMixer)
                    ?? throw new ConfigValidationException(
                        $"{entry}.videoMixer",
                        $"no video mixer is configured with instance {config.VideoMixer}");

        InterfaceValidation.Bindings(entry, config);

        var device = new ControlSurfaceDevice(entry.Instance);
        surfaces.Add(device);
        return Task.FromResult<IHmi>(new Gamepad(
            config,
            device,
            mixer,
            cameras.Get,
            logger,
            functions: device.FunctionRequested,
            inputs: device.InputRequested));
    }
}
