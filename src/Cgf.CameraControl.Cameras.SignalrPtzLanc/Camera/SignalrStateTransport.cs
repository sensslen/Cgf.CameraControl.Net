using System.Net.Http.Json;
using System.Reactive.Subjects;
using System.Text.Json;
using Cgf.CameraControl.Core.Logger;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;

/// Replaces the @microsoft/signalr client half of the TypeScript camera.
///
/// Reaching the controller is two steps. Its REST side lists the serial ports it can drive and is
/// told which one this camera claims; only then does the hub carry state. Both steps run again after
/// a reconnect, because a controller that restarted has forgotten the claim.
public sealed class SignalrStateTransport : ISignalrStateTransport
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private readonly string _url;
    private readonly string _port;
    private readonly ILogger _logger;
    private readonly HttpClient _http;
    private readonly HubConnection _hub;
    private readonly BehaviorSubject<bool> _connected = new(false);
    private readonly CancellationTokenSource _stopping = new();

    private Task? _loop;

    public SignalrStateTransport(string url, string port, ILogger logger)
    {
        _url = url;
        _port = port;
        _logger = logger;

        _http = new HttpClient(AcceptAnyCertificate());
        _hub = new HubConnectionBuilder()
            .WithUrl($"{url}/statehub", options => options.HttpMessageHandlerFactory = _ => AcceptAnyCertificate())
            .WithAutomaticReconnect()
            .AddJsonProtocol(options => ConfigurePayload(options.PayloadSerializerOptions))
            .Build();

        _hub.Reconnecting += _ =>
        {
            Log("connection error - trying automatic reconnect");
            Report(connected: false);
            return Task.CompletedTask;
        };

        _hub.Reconnected += async _ =>
        {
            Log("reconnect successful");
            if (await ClaimPortAsync(_stopping.Token).ConfigureAwait(false))
            {
                Report(connected: true);
            }
        };
    }

    public IObservable<bool> WhenConnectedChanged => _connected;

    /// NativeAOT has no reflection based serializer to fall back on, so the hub is given the
    /// generated one for the types this camera exchanges. The aot-probe drives the same call, which
    /// is why it is here rather than inline in the builder.
    public static void ConfigurePayload(JsonSerializerOptions options) =>
        options.TypeInfoResolverChain.Insert(0, SignalrPtzLancCameraStateContext.Default);

    public void Start() => _loop ??= Task.Run(() => ConnectAsync(_stopping.Token));

    public async Task<bool> SetStateAsync(SignalrPtzLancCameraState state, CancellationToken cancellationToken) =>
        await _hub.InvokeAsync<bool>("SetState", state, cancellationToken).ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected while shutting down.
            }
        }

        await _hub.DisposeAsync().ConfigureAwait(false);
        _http.Dispose();
        _stopping.Dispose();
        _connected.Dispose();
    }

    /// The controllers this talks to sit on a closed network behind self-signed certificates, which
    /// is what the TypeScript camera's undici agent allows too. Nothing secret travels this
    /// connection: it carries stick positions in one direction and an acknowledgement back.
    private static HttpMessageHandler AcceptAnyCertificate() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
    };

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!await ClaimPortAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            try
            {
                await _hub.StartAsync(cancellationToken).ConfigureAwait(false);
                Report(connected: true);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogError($"Socket connection setup failed with error:{ex.Message} - retrying");
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// False once the controller has been reached and has said it cannot drive this port. That is
    /// not something a retry fixes, so the camera stops rather than claiming a port it does not have.
    private async Task<bool> ClaimPortAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string[]? available;
            try
            {
                available = await _http
                    .GetFromJsonAsync($"{_url}/connections", SignalrPtzLancCameraStateContext.Default.StringArray, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex)
            {
                Log($"Failed to connect - {ex.Message}");
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (available is null || !available.Contains(_port))
            {
                LogError($"Port:{_port} is not available. Available Ports:{string.Join(", ", available ?? [])}");
                LogError("Stopping camera.");
                return false;
            }

            try
            {
                var response = await _http
                    .PutAsJsonAsync(
                        $"{_url}/connection",
                        new ControllerConnection(_port, true),
                        SignalrPtzLancCameraStateContext.Default.ControllerConnection,
                        cancellationToken)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex)
            {
                LogError($"Failed to connect to Port:{_port} with error:{ex.Message}");
                LogError("Stopping camera.");
                return false;
            }
        }

        return false;
    }

    private void Report(bool connected)
    {
        if (_connected.Value != connected)
        {
            _connected.OnNext(connected);
        }
    }

    private void Log(string message) => _logger.Log($"CgfPtzCamera({_url}):{message}");

    private void LogError(string message) => _logger.Error($"CgfPtzCamera({_url}):{message}");
}
