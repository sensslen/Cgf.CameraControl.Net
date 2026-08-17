using System.Reactive.Subjects;
using System.Text.Json;
using Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;

namespace Cgf.CameraControl.Cameras.WebsocketPtzLanc.Tests;

public sealed class FakeWebsocketTransport : IWebsocketTransport
{
    private readonly BehaviorSubject<bool> _connected = new(false);
    private readonly Subject<string> _received = new();

    public bool Started { get; private set; }

    public List<WebsocketPtzLancCameraSpeedState> Sent { get; } = [];

    public IObservable<bool> WhenConnectedChanged => _connected;

    public IObservable<string> Received => _received;

    public void Start() => Started = true;

    public void Send(string payload) => Sent.Add(Parse(payload));

    /// Replays what the controller firmware echoes back after acting on a request.
    public void Echo(WebsocketPtzLancCameraSpeedState state) =>
        _received.OnNext(JsonSerializer.Serialize(state, WebsocketPtzLancCameraStateContext.Default.WebsocketPtzLancCameraSpeedState));

    public void EchoRaw(string payload) => _received.OnNext(payload);

    /// The controller reports whatever it is doing, so echoing the last request opens the gate.
    public void EchoLastSend() => Echo(Sent[^1]);

    public ValueTask DisposeAsync()
    {
        _connected.Dispose();
        _received.Dispose();
        return ValueTask.CompletedTask;
    }

    private static WebsocketPtzLancCameraSpeedState Parse(string payload) =>
        JsonSerializer.Deserialize(payload, WebsocketPtzLancCameraStateContext.Default.WebsocketPtzLancCameraSpeedState);
}
