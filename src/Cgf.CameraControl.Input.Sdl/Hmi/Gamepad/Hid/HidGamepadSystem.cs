using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Hid;

public sealed class HidGamepadSystem(ILogger logger, IHidGamepadSource? source = null) : IAsyncDisposable
{
    private readonly IHidGamepadSource _source = source ?? new HidGamepadSource();
    private readonly Dictionary<HidGamepadDevice, IDisposable?> _devices = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<string> SerialNumbers => _source.SerialNumbers;

    /// The reservation is held for as long as the device is, and released with it.
    public IGamepadDevice Claim(string label, string serial, double deadzone, IDisposable? reservation = null)
    {
        lock (_gate)
        {
            if (_devices.Keys.Any(device => GamepadClaims.Matches(serial, device.SerialNumber)))
            {
                reservation?.Dispose();
                throw new InvalidOperationException($"HID serial {serial} is already claimed by another interface.");
            }
            var claimed = new HidGamepadDevice(_source, label, serial, deadzone, logger, Release);
            _devices.Add(claimed, reservation);
            return claimed;
        }
    }

    public async ValueTask DisposeAsync()
    {
        HidGamepadDevice[] devices;
        lock (_gate)
        {
            devices = _devices.Keys.ToArray();
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
            if (_devices.Remove(device, out var reservation))
            {
                reservation?.Dispose();
            }
        }
    }
}
