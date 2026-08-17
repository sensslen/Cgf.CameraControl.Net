namespace Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;

/// The TypeScript camera drives websocket-reconnect directly. The socket sits behind this seam here
/// so the echo gated send protocol, which is the part that is easy to get wrong, can be tested
/// without a camera on the network.
public interface IWebsocketTransport : IAsyncDisposable
{
    IObservable<bool> WhenConnectedChanged { get; }

    IObservable<string> Received { get; }

    void Start();

    void Send(string payload);
}
