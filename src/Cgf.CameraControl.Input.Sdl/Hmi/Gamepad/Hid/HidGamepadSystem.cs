using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Hid;

public sealed class HidGamepadSystem(ILogger logger, IHidGamepadSource? source = null) : IAsyncDisposable
{
    private readonly IHidGamepadSource _source = source ?? new HidGamepadSource();
    private readonly List<HidGamepadDevice> _devices = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<string> SerialNumbers => _source.SerialNumbers;

    public IGamepadDevice Claim(string label, string serial, double deadzone)
    {
        lock (_gate)
        {
            if (_devices.Any(device => GamepadClaims.Matches(serial, device.SerialNumber)))
            {
                throw new InvalidOperationException($"HID serial {serial} is already claimed by another interface.");
            }
            var claimed = new HidGamepadDevice(_source, label, serial, deadzone, logger, Release);
            _devices.Add(claimed);
            return claimed;
        }
    }

    public async ValueTask DisposeAsync()
    {
        HidGamepadDevice[] devices;
        lock (_gate)
        {
            devices = _devices.ToArray();
        }
        foreach (var device in devices)
        {
            await device.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void Release(HidGamepadDevice device)
    {
        lock (_gate)
        {
            _devices.Remove(device);
        }
    }
}
