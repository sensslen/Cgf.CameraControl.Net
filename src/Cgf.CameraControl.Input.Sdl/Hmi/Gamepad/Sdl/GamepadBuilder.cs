using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Hmi;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Hid;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;

public sealed class GamepadBuilder(
    SdlGamepadSystem system,
    VideoMixerFactory mixers,
    CameraConnectionFactory cameras,
    ILogger logger,
    HidGamepadSystem? hid = null) : IBuilder<IHmi, InterfaceConfiguration>
{
    /// The legacy pad records derive from GamepadConfiguration, so one claim covers them all.
    public Task<IHmi>? BuildAsync(InterfaceConfiguration entry, CancellationToken cancellationToken) =>
        entry is GamepadConfiguration config ? BuildAsync(config, cancellationToken) : null;

    private async Task<IHmi> BuildAsync(GamepadConfiguration config, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var mixer = mixers.Get(config.VideoMixer)
                    ?? throw new ConfigValidationException(
                        $"{config}.videoMixer",
                        $"no video mixer is configured with instance {config.VideoMixer}");

        InterfaceValidation.Bindings(config);

        var deadzone = config.Deadzone ?? GamepadConfiguration.DefaultDeadzone;
        IGamepadDevice device;
        if (config.Backend == GamepadBackend.Hid)
        {
            if (hid is null)
            {
                throw new ConfigValidationException($"{config}.backend", "HID backend is not registered.");
            }
            var serial = config.SerialNumber
                ?? throw new ConfigValidationException($"{config}.serialNumber", "HID backend requires a serial number.");
            device = hid.Claim(config.ToString(), serial, deadzone);
        }
        else if (config.Backend is null or GamepadBackend.Sdl)
        {
            device = system.Claim(config.ToString(), config.SerialNumber, deadzone, config.Rumble ?? true);
        }
        else
        {
            throw new ConfigValidationException($"{config}.backend", $"Unknown gamepad backend {config.Backend}.");
        }
        try
        {
            return new Shared.Gamepad(config, device, mixer, cameras.Get, logger, config.Pad);
        }
        catch
        {
            await device.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
