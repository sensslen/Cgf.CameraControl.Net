namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

public sealed class DirectionalConnectionChange(DirectionalConnectionChangeConfiguration config) : IConnectionChange
{
    public int? Next(ButtonDirection direction, int currentSelection, AltKeyConfiguration altKeys)
    {
        if (!config.Directions.TryGetValue(currentSelection, out var map))
        {
            // JavaScript enumerates integer-like object keys in ascending order, so the TypeScript
            // fallback of "the first key" is the lowest one. Dictionary has no such ordering, so the
            // minimum is taken explicitly rather than relying on enumeration.
            if (config.Directions.Count == 0)
            {
                return null;
            }

            map = config.Directions[config.Directions.Keys.Min()];
        }

        return map.TryGetValue(direction, out var input) ? input : null;
    }
}
