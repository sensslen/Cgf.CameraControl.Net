namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

public sealed class DirectConnectionChange(DirectConnectionChangeConfiguration config) : IConnectionChange
{
    public int? Next(ButtonDirection direction, int currentSelection, AltKeyConfiguration altKeys)
    {
        var set = altKeys switch
        {
            { Alt: true } when config.Alt is not null => config.Alt,
            { AltLower: true } when config.AltLower is not null => config.AltLower,
            _ => config.Default,
        };

        return set.TryGetValue(direction, out var input) ? input : null;
    }
}
