using System.Reactive.Subjects;
using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;

namespace Cgf.CameraControl.Cameras.ViscaOverIp.Tests;

public sealed class FakeViscaTransport : IViscaTransport
{
    private readonly BehaviorSubject<bool> _connected = new(false);
    private readonly Subject<byte[]> _received = new();

    public bool Started { get; private set; }

    public List<byte[]> Sent { get; } = [];

    public IObservable<bool> WhenConnectedChanged => _connected;

    public IObservable<byte[]> Received => _received;

    public void Start() => Started = true;

    public void Send(byte[] packet) => Sent.Add(packet);

    public void Connect() => _connected.OnNext(true);

    public void Disconnect() => _connected.OnNext(false);

    /// 90 4y FF, the camera taking the command into buffer y.
    public void Acknowledge(int socket = 1) => _received.OnNext([0x90, (byte)(0x40 | socket), 0xFF]);

    /// 90 5y FF, the command in buffer y having run.
    public void Complete(int socket = 1) => _received.OnNext([0x90, (byte)(0x50 | socket), 0xFF]);

    /// 90 6y 03 FF, the camera refusing the command in buffer y because its buffers are full.
    public void Refuse(int socket = 1) => _received.OnNext([0x90, (byte)(0x60 | socket), 0x03, 0xFF]);

    public void Receive(params byte[] datagram) => _received.OnNext(datagram);

    public ValueTask DisposeAsync()
    {
        _connected.Dispose();
        _received.Dispose();
        return ValueTask.CompletedTask;
    }
}
