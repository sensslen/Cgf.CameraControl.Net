using SDL3;

namespace Cgf.CameraControl.Input.Sdl;

public static class AotProbe
{
    public static string Touch()
    {
        SDL.SetHint(SDL.Hints.JoystickAllowBackgroundEvents, "1");
        if (!SDL.Init(SDL.InitFlags.Gamepad))
        {
            return $"init failed: {SDL.GetError()}";
        }

        var ids = SDL.GetGamepads(out var count) ?? [];
        var names = ids.Take(count).Select(id => SDL.GetGamepadNameForID(id) ?? "?").ToList();

        SDL.Quit();
        return $"{names.Count} pad(s): {string.Join(", ", names)}";
    }
}
