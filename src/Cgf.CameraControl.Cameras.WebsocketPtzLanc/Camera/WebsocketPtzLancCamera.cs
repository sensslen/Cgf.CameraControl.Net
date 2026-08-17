using System.Text.Json;
using Cgf.CameraControl.Core.CameraConnection;

namespace Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;

public sealed class WebsocketPtzLancCamera : ICameraConnection
{
    private readonly WebsocketPtzLancCameraConfiguration _config;
    private readonly IWebsocketTransport _transport;
    private readonly Lock _gate = new();
    private readonly IDisposable _subscription;

    private WebsocketPtzLancCameraSpeedState _requested;
    private bool _canSend;

    public WebsocketPtzLancCamera(WebsocketPtzLancCameraConfiguration config, IWebsocketTransport transport)
    {
        _config = config;
        _transport = transport;
        _subscription = transport.Received.Subscribe(OnReceived);
        transport.Start();
    }

    public string ConnectionString => $"ws://{_config.Ip}/ws";

    public IObservable<bool> WhenConnectedChanged => _transport.WhenConnectedChanged;

    public void Pan(double value) => SetState(state => state with
    {
        Pan = InvertIfNecessary(MultiplyRoundAndCrop(Interpolate(value) * 255, 255)),
    });

    public void Tilt(double value) => SetState(state => state with
    {
        Tilt = InvertIfNecessary(MultiplyRoundAndCrop(Interpolate(value) * 255, 255)),
    });

    // The response curve is deliberately applied to pan and tilt only.
    public void Zoom(double value) => SetState(state => state with
    {
        Zoom = MultiplyRoundAndCrop(value * 255, 255),
    });

    public void Focus(double value)
    {
    }

    public void SetTally(TallyState value)
    {
        if (!_config.ShowTallyLight)
        {
            return;
        }

        SetState(state => value switch
        {
            TallyState.Preview => state with { Green = 255, Red = 0 },
            TallyState.Program => state with { Green = 0, Red = 255 },
            _ => state with { Green = 0, Red = 0 },
        });
    }

    public async ValueTask DisposeAsync()
    {
        _subscription.Dispose();
        await _transport.DisposeAsync().ConfigureAwait(false);
    }

    private void SetState(Func<WebsocketPtzLancCameraSpeedState, WebsocketPtzLancCameraSpeedState> change)
    {
        WebsocketPtzLancCameraSpeedState toSend;
        lock (_gate)
        {
            _requested = change(_requested);
            if (!_canSend)
            {
                return;
            }

            _canSend = false;
            toSend = _requested;
        }

        Send(toSend);
    }

    /// The camera echoes the state it is acting on. A matching echo means the request has landed and
    /// the next one may go out; anything else means the camera is behind, so the request is repeated.
    private void OnReceived(string payload)
    {
        WebsocketPtzLancCameraSpeedState echoed;
        try
        {
            echoed = JsonSerializer.Deserialize(payload, WebsocketPtzLancCameraStateContext.Default.WebsocketPtzLancCameraSpeedState);
        }
        catch (JsonException)
        {
            return;
        }

        WebsocketPtzLancCameraSpeedState toSend;
        lock (_gate)
        {
            if (echoed.Matches(_requested, _config.ShowTallyLight))
            {
                _canSend = true;
                return;
            }

            _canSend = false;
            toSend = _requested;
        }

        Send(toSend);
    }

    private void Send(WebsocketPtzLancCameraSpeedState state) =>
        _transport.Send(JsonSerializer.Serialize(state, WebsocketPtzLancCameraStateContext.Default.WebsocketPtzLancCameraSpeedState));

    private int InvertIfNecessary(int value) => _config.PanTiltInvert ? -value : value;

    /// Squares the stick position while keeping its sign, so small movements are fine grained.
    private static double Interpolate(double value) => value > 0 ? value * value : -(value * value);

    private static int MultiplyRoundAndCrop(double value, int maximumAbsolute) =>
        (int)Math.Round(Math.Clamp(value, -maximumAbsolute, maximumAbsolute));
}
