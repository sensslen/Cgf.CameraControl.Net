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
        Names(config, "pad.default", config.Pad?.Default?.Values);
        Names(config, "pad.alt", config.Pad?.Alt?.Values);
        Names(config, "pad.altLower", config.Pad?.AltLower?.Values);
    }

    public static void Bindings(KeyboardConfiguration config) =>
        Names(config, "keys.function", config.Keys.Function?.Values);

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
