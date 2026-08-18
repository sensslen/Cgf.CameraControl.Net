namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

/// The TypeScript factory re-parses the entry and can fail. Here the configuration has already been
/// validated into a closed set of types, so this only has to pick the implementation.
public static class ConnectionChangeFactory
{
    public static IConnectionChange Get(ConnectionChangeConfiguration config) => config switch
    {
        DirectConnectionChangeConfiguration direct => new DirectConnectionChange(direct),
        DirectionalConnectionChangeConfiguration directional => new DirectionalConnectionChange(directional),
        _ => throw new ArgumentOutOfRangeException(nameof(config), config, "unhandled connection change"),
    };
}
