namespace Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;

/// The TypeScript camera drives the SignalR hub connection directly. The hub sits behind this seam
/// here so the transmission gate, which is the part that is easy to get wrong, can be tested without
/// a controller on the network.
public interface ISignalrStateTransport : IAsyncDisposable
{
    IObservable<bool> WhenConnectedChanged { get; }

    void Start();

    /// False when the controller refused the update and it should be sent again.
    Task<bool> SetStateAsync(SignalrPtzLancCameraState state, CancellationToken cancellationToken);
}
