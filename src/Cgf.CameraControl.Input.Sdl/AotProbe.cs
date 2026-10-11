using SDL3;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Hid;

namespace Cgf.CameraControl.Input.Sdl;

public static class AotProbe
{
    public static string Touch()
    {
        SDL.SetHint(SDL.Hints.JoystickAllowBackgroundEvents, "1");
        if (OperatingSystem.IsMacOS())
        {
            SDL.SetHint(SDL.Hints.JoystickMFI, "0");
        }
        if (!SDL.Init(SDL.InitFlags.Gamepad))
        {
            return $"init failed: {SDL.GetError()}";
        }

        var ids = SDL.GetGamepads(out var count) ?? [];
        var names = ids.Take(count).Select(id => SDL.GetGamepadNameForID(id) ?? "?").ToList();

        SDL.Quit();
        var source = new HidGamepadSource();
        var serials = source.SerialNumbers;
        foreach (var serial in serials)
        {
            using var connection = source.Open(serial);
            connection.Read(new byte[64]);
        }
        return $"{names.Count} pad(s): {string.Join(", ", names)}; HID F310 serials: {string.Join(", ", serials)}";
    }
}
