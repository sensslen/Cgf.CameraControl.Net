using System.Reactive;
using System.Reactive.Subjects;
using AtemSharp;
using AtemSharp.Commands;
using AtemSharp.DependencyInjection;
using AtemSharp.State;
using AtemSharp.State.Macro;
using Cgf.CameraControl.Core.Logger;
using Library = Microsoft.Extensions.DependencyInjection.Library;

namespace Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;

public interface IAtemConnectionFactory
{
    IAtemConnection Get(string ip);

    Task ReleaseAsync(string ip);
}

public interface IAtemConnection
{
    /// A mirror of the switcher state, applied from the same command stream the switcher consumes.
    /// AtemSharp's state objects raise nothing when they change, so this copy exists to give the
    /// mixer a deterministic point at which to diff. Reads go here, never to switcher.State, and
    /// run under the lock that applies incoming commands, so nothing changes mid-read.
    T Read<T>(Func<AtemState, T> read);

    bool Connected { get; }

    IObservable<bool> WhenConnectionChanged { get; }

    IObservable<Unit> WhenStateChanged { get; }

    Task RunMacroAsync(int macro);

    Task StartupAsync(CancellationToken cancellationToken);

    Task SendAsync(SerializedCommand command);
}

internal sealed class AtemConnection : IAtemConnection, IAsyncDisposable
{
    private const int AtemPort = 9910;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private readonly string _ip;
    private readonly ILogger _logger;
    private readonly IServices _services;
    private readonly Type? _initCompleteType;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Lock _stateLock = new();
    private readonly BehaviorSubject<bool> _connected = new(false);
    private readonly Subject<Unit> _stateChanged = new();
    private volatile IAtemSwitcher? _switcher;
    private Task? _startup;
    private bool _initialized;

    public AtemConnection(string ip, ILogger logger, IServices services)
    {
        _ip = ip;
        _logger = logger;
        _services = services;
        // InitCompleteCommand is internal to AtemSharp, but the parser exposes its protocol identity.
        _initCompleteType = services.CreateCommandParser().ParseCommand("InCm", [])?.GetType();
    }

    private AtemState State { get; set; } = new();

    public T Read<T>(Func<AtemState, T> read)
    {
        lock (_stateLock)
        {
            return read(State);
        }
    }

    public bool Connected => _connected.Value;

    public IObservable<bool> WhenConnectionChanged => _connected;

    public IObservable<Unit> WhenStateChanged => _stateChanged;

    /// Running a macro needs a Macro instance carrying the right id, and Macro.Id has an internal
    /// setter, so the switcher's own macro system is the only way to reach one.
    public async Task RunMacroAsync(int macro)
    {
        try
        {
            if (Connected && _switcher?.Macros.FirstOrDefault(m => m.Id == macro) is { } found)
            {
                await found.Run().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Error($"running macro {macro} failed - {ex.Message}");
        }
    }

    public Task StartupAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_stateLock)
        {
            _startup ??= Task.Run(() => ConnectAsync(_stopping.Token));
        }
        return Task.CompletedTask;
    }

    public async Task SendAsync(SerializedCommand command)
    {
        try
        {
            if (Connected && _switcher is { } switcher)
            {
                await switcher.SendCommandAsync(command).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Error($"sending {command.GetType().Name} failed - {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        if (_startup is not null)
        {
            await _startup.ConfigureAwait(false);
        }

        Report(false);
        _stopping.Dispose();
        _connected.Dispose();
        _stateChanged.Dispose();
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var reportedFailure = false;
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var services = new ObservingServices(_services, Apply, Reconnecting, cancellationToken);
            await using var switcher = new Library(services).CreateAtemSwitcher(_ip, AtemPort);
            _switcher = switcher;
            try
            {
                Log($"connecting to UDP port {AtemPort}");
                await switcher.ConnectAsync(cancellationToken).ConfigureAwait(false);
                reportedFailure = false;
                lock (_stateLock)
                {
                    Report(_initialized);
                }
                // AtemSharp's protocol owns reconnects; the switcher exposes neither a disconnect
                // event nor state transitions for them, so do not start a competing retry loop.
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                if (!reportedFailure)
                {
                    Error($"connection failed - {ex.Message}");
                    reportedFailure = true;
                }
            }
            finally
            {
                Report(false);
                _switcher = null;
            }

            try
            {
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    // The switcher replays its whole state on connect, so this also seeds State.
    private void Apply(IDeserializedCommand command)
    {
        lock (_stateLock)
        {
            if (_stopping.IsCancellationRequested)
            {
                return;
            }

            try
            {
                command.ApplyToState(State);
                _stateChanged.OnNext(Unit.Default);
                if (command.GetType() == _initCompleteType)
                {
                    _initialized = true;
                    if (_switcher?.ConnectionState == ConnectionState.Connected)
                    {
                        Report(true);
                    }
                }
            }
            catch (Exception ex)
            {
                Error($"applying {command.GetType().Name} failed - {ex.Message}");
            }
        }
    }

    private void Reconnecting()
    {
        lock (_stateLock)
        {
            Report(false);
            _initialized = false;
            State = new AtemState();
            _stateChanged.OnNext(Unit.Default);
        }
    }

    private void Report(bool connected)
    {
        lock (_stateLock)
        {
            if (_connected.Value != connected)
            {
                _connected.OnNext(connected);
                Log(connected ? "connected" : "disconnected");
            }
        }
    }

    private void Log(string message) => _logger.Log("Atem", $"({_ip}) {message}");

    private void Error(string message) => _logger.Error("Atem", $"({_ip}) {message}");
}

/// One connection per switcher address, shared by every mix effect block configured against it.
/// ensi.json points two mixer instances at one switcher, so this is load bearing.
public sealed class AtemFactory(ILogger logger, IServices services) : IAtemConnectionFactory
{
    private readonly Dictionary<string, Shared> _connections = new(StringComparer.OrdinalIgnoreCase);

    public IAtemConnection Get(string ip)
    {
        if (_connections.TryGetValue(ip, out var existing))
        {
            existing.Usages++;
            return existing.Connection;
        }

        var connection = new AtemConnection(ip, logger, services);
        _connections[ip] = new Shared(connection);
        return connection;
    }

    public async Task ReleaseAsync(string ip)
    {
        if (!_connections.TryGetValue(ip, out var shared))
        {
            return;
        }

        shared.Usages--;
        if (shared.Usages > 0)
        {
            return;
        }

        _connections.Remove(ip);
        await shared.Connection.DisposeAsync().ConfigureAwait(false);
    }

    private sealed class Shared(AtemConnection connection)
    {
        public AtemConnection Connection => connection;

        public int Usages { get; set; } = 1;
    }
}
