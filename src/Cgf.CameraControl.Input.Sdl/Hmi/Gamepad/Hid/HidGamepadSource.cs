using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;
using HidSharp;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Hid;

public interface IHidGamepadConnection : IDisposable
{
    string Description { get; }
    int Read(byte[] buffer);
}

public interface IHidGamepadSource
{
    IReadOnlyList<string> SerialNumbers { get; }
    IHidGamepadConnection Open(string serialNumber);
}

public sealed class HidGamepadSource : IHidGamepadSource
{
    // Validate the layout rather than decoding a different firmware's report as an F310.
    private static readonly byte[] Descriptor = Convert.FromHexString(
        "05010904A101A10275089504150026FF00350046FF0009300931093209358102750495012507463B0165140939814265007501950C2501450105091901290C81020600FF750195102501450109018102C0A1027508950746FF0026FF0009029102C0C0");

    public IReadOnlyList<string> SerialNumbers => DeviceList.Local.GetHidDevices(0x046d, 0xc216)
        .Select(device => device.GetSerialNumber()).Where(serial => !string.IsNullOrWhiteSpace(serial))
        .Distinct(StringComparer.Ordinal).ToArray();

    public IHidGamepadConnection Open(string serialNumber)
    {
        var matches = DeviceList.Local.GetHidDevices(0x046d, 0xc216)
            .Where(device => GamepadClaims.Matches(serialNumber, device.GetSerialNumber())).ToArray();
        if (matches.Length != 1)
        {
            throw new IOException(matches.Length == 0
                ? $"No HID gamepad reports serial {serialNumber}."
                : $"Serial {serialNumber} is ambiguous: {matches.Length} HID devices report it.");
        }

        var device = matches[0];
        if (device.VendorID != 0x046d || device.ProductID != 0xc216 ||
            !device.GetRawReportDescriptor().AsSpan().SequenceEqual(Descriptor))
        {
            throw new NotSupportedException($"HID backend supports the Logitech F310 in D mode only ({serialNumber}).");
        }
        return new Connection(device);
    }

    private sealed class Connection : IHidGamepadConnection
    {
        private readonly HidStream _stream;

        public Connection(HidDevice device)
        {
            Description = $"{device.GetProductName()} ({device.GetSerialNumber()})";
            _stream = device.Open();
            _stream.ReadTimeout = 100;
        }

        public string Description { get; }

        public int Read(byte[] buffer)
        {
            try
            {
                return _stream.Read(buffer);
            }
            catch (TimeoutException)
            {
                return 0;
            }
        }

        public void Dispose() => _stream.Dispose();
    }
}
