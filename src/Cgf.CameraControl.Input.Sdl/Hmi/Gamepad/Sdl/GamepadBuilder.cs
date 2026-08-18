using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Hmi;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;

public sealed class GamepadBuilder(
    SdlGamepadSystem system,
    VideoMixerFactory mixers,
    CameraConnectionFactory cameras,
    ILogger logger) : IBuilder<IHmi>
{
    /// SDL normalises every pad to one button and axis layout, so the controller model no longer
    /// selects any behaviour and all three Logitech strings build the same thing. They stay accepted
    /// so existing configuration files keep loading.
    public IReadOnlyCollection<string> SupportedTypes =>
        ["gamepad", "logitech/F310", "logitech/F710", "logitech/Rumblepad2"];

    public Task<IHmi> BuildAsync(ConfigEntry entry, CancellationToken cancellationToken)
    {
        var config = entry.Deserialize(GamepadConfigurationContext.Default.GamepadConfiguration);
        var mixer = mixers.Get(config.VideoMixer)
                    ?? throw new ConfigValidationException(
                        $"{entry}.videoMixer",
                        $"no video mixer is configured with instance {config.VideoMixer}");

        var device = system.Claim(entry.ToString(), config.SerialNumber, config.Deadzone);
        return Task.FromResult<IHmi>(new Shared.Gamepad(config, device, mixer, cameras.Get, logger));
    }
}
