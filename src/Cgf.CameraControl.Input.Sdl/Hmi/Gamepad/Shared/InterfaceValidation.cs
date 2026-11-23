using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

/// A binding names a function, and a name is a string, so nothing in the schema stops it naming one
/// that is not there. Caught at load, it points at the line; caught at run time it is a button that
/// silently does nothing in the middle of a service.
public static class InterfaceValidation
{
    public static void Bindings(GamepadConfiguration config)
    {
        if (config.Backend is { } backend && !Enum.IsDefined(backend))
        {
            throw new ConfigValidationException($"{config}.backend", $"Unknown gamepad backend {backend}.");
        }
        if (config.Backend == GamepadBackend.Hid)
        {
            if (string.IsNullOrWhiteSpace(config.SerialNumber) || !config.SerialNumber.Any(char.IsLetterOrDigit))
            {
                throw new ConfigValidationException($"{config}.serialNumber", "HID backend requires a serial number.");
            }
            if (!config.SupportsHid)
            {
                throw new ConfigValidationException($"{config}.backend", "HID backend currently supports F310 pads only.");
            }
        }
        Names(config, "pad.default", config.Pad?.Default?.Values);
        Names(config, "pad.alt", config.Pad?.Alt?.Values);
        Names(config, "pad.altLower", config.Pad?.AltLower?.Values);
        Shared(config);
    }

    public static void Bindings(KeyboardConfiguration config)
    {
        Names(config, "keys.function", config.Keys.Function?.Values);
        Shared(config);
    }

    private static void Shared(ControlInterfaceConfiguration config)
    {
        Validate.NonNegativeMap(config.CameraMap, $"{config}.cameraMap");
        foreach (var (name, function) in config.Functions ?? new Dictionary<string, SpecialFunctionConfiguration>())
        {
            // An empty loop is a button that logs an error on every press instead of running a macro.
            if (function is MacroLoopSpecialFunctionConfiguration loop)
            {
                Validate.MinCount(loop.Indexes, 1, $"{config}.functions.{name}.indexes");
                foreach (var index in loop.Indexes)
                {
                    Validate.NonNegative(index, $"{config}.functions.{name}.indexes");
                }
            }
        }
    }

    private static void Names(
        ControlInterfaceConfiguration config,
        string path,
        IEnumerable<string>? bound)
    {
        var functions = config.Functions ?? new Dictionary<string, SpecialFunctionConfiguration>();
        foreach (var name in bound ?? [])
        {
            if (!functions.ContainsKey(name))
            {
                throw new ConfigValidationException(
                    $"{config}.{path}",
                    $"binds {name}, which is not one of the configured functions" +
                    (functions.Count == 0
                        ? " (none are configured)"
                        : $" ({string.Join(", ", functions.Keys.Order())})"));
            }
        }
    }
}
