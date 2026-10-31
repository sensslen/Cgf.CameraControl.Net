using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

/// A binding names a function, and a name is a string, so nothing in the schema stops it naming one
/// that is not there. Caught at load, it points at the line; caught at run time it is a button that
/// silently does nothing in the middle of a service.
public static class InterfaceValidation
{
    public static void Bindings(ConfigEntry entry, GamepadConfiguration config)
    {
        if (!Enum.IsDefined(config.Backend))
        {
            throw new ConfigValidationException($"{entry}.backend", $"Unknown gamepad backend {config.Backend}.");
        }
        if (config.Backend == GamepadBackend.Hid)
        {
            if (string.IsNullOrWhiteSpace(config.SerialNumber) || !config.SerialNumber.Any(char.IsLetterOrDigit))
            {
                throw new ConfigValidationException($"{entry}.serialNumber", "HID backend requires a serial number.");
            }
            if (entry.Type.Equals("logitech/F710", StringComparison.OrdinalIgnoreCase) ||
                entry.Type.Equals("logitech/Rumblepad2", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConfigValidationException($"{entry}.backend", "HID backend currently supports F310 pads only.");
            }
        }
        Names(entry, config, "pad.default", config.Pad.Default.Values);
        Names(entry, config, "pad.alt", config.Pad.Alt?.Values);
        Names(entry, config, "pad.altLower", config.Pad.AltLower?.Values);
        Shared(entry, config);
    }

    public static void Bindings(ConfigEntry entry, KeyboardConfiguration config)
    {
        Names(entry, config, "keys.function", config.Keys.Function?.Values);
        Shared(entry, config);
    }

    private static void Shared(ConfigEntry entry, InterfaceConfiguration config)
    {
        Validate.NonNegativeMap(config.CameraMap, $"{entry}.cameraMap");
        foreach (var (name, function) in config.Functions)
        {
            // An empty loop is a button that logs an error on every press instead of running a macro.
            if (function is MacroLoopSpecialFunctionConfiguration loop)
            {
                Validate.MinCount(loop.Indexes, 1, $"{entry}.functions.{name}.indexes");
                foreach (var index in loop.Indexes)
                {
                    Validate.NonNegative(index, $"{entry}.functions.{name}.indexes");
                }
            }
        }
    }

    private static void Names(
        ConfigEntry entry,
        InterfaceConfiguration config,
        string path,
        IEnumerable<string>? bound)
    {
        foreach (var name in bound ?? [])
        {
            if (!config.Functions.ContainsKey(name))
            {
                throw new ConfigValidationException(
                    $"{entry}.{path}",
                    $"binds {name}, which is not one of the configured functions" +
                    (config.Functions.Count == 0
                        ? " (none are configured)"
                        : $" ({string.Join(", ", config.Functions.Keys.Order())})"));
            }
        }
    }
}
