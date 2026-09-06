using Cgf.CameraControl.Core.Configuration;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

/// A binding names a function, and a name is a string, so nothing in the schema stops it naming one
/// that is not there. Caught at load, it points at the line; caught at run time it is a button that
/// silently does nothing in the middle of a service.
public static class InterfaceValidation
{
    public static void Bindings(ConfigEntry entry, GamepadConfiguration config)
    {
        Names(entry, config, "pad.default", config.Pad.Default.Values);
        Names(entry, config, "pad.alt", config.Pad.Alt?.Values);
        Names(entry, config, "pad.altLower", config.Pad.AltLower?.Values);
    }

    public static void Bindings(ConfigEntry entry, KeyboardConfiguration config) =>
        Names(entry, config, "keys.function", config.Keys.Function?.Values);

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
