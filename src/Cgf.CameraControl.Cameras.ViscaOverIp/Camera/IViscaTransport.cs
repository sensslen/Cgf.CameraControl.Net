namespace Cgf.CameraControl.Cameras.ViscaOverIp.Camera;

/// The TypeScript camera drives node-visca-over-ip directly. The datagram socket sits behind this
/// seam here so the command gate, which is the part that is easy to get wrong, can be tested
/// without a camera on the network.
public interface IViscaTransport : IAsyncDisposable
{
    IObservable<bool> WhenConnectedChanged { get; }

    IObservable<byte[]> Received { get; }

    void Start();

    void Send(byte[] packet);
}
