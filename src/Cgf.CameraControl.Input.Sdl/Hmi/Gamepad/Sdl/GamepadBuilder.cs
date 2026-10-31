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
    HidGamepadSystem? hid = null) : IBuilder<IHmi>
{
    /// The legacy names still select gamepad interfaces. HID decoding is explicitly opt-in;
    /// existing configurations retain SDL's controller mappings.
    public IReadOnlyCollection<string> SupportedTypes =>
        ["gamepad", "logitech/F310", "logitech/F710", "logitech/Rumblepad2"];

    public async Task<IHmi> BuildAsync(ConfigEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var config = entry.Deserialize(InterfaceConfigurationContext.Default.GamepadConfiguration);
        var mixer = mixers.Get(config.VideoMixer)
                    ?? throw new ConfigValidationException(
                        $"{entry}.videoMixer",
                        $"no video mixer is configured with instance {config.VideoMixer}");

        InterfaceValidation.Bindings(entry, config);

        IGamepadDevice device;
        if (config.Backend == GamepadBackend.Hid)
        {
            if (hid is null)
            {
                throw new ConfigValidationException($"{entry}.backend", "HID backend is not registered.");
            }
            var serial = config.SerialNumber
                ?? throw new ConfigValidationException($"{entry}.serialNumber", "HID backend requires a serial number.");
            device = hid.Claim(entry.ToString(), serial, config.Deadzone, system.Reserve(serial));
        }
        else if (config.Backend == GamepadBackend.Sdl)
        {
            device = system.Claim(entry.ToString(), config.SerialNumber, config.Deadzone, config.Rumble);
        }
        else
        {
            throw new ConfigValidationException($"{entry}.backend", $"Unknown gamepad backend {config.Backend}.");
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
