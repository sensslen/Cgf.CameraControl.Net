using System.Reactive;
using System.Reactive.Subjects;
using System.Threading.Tasks.Dataflow;
using AtemSharp;
using AtemSharp.Commands;
using AtemSharp.Communication;
using AtemSharp.DependencyInjection;
using AtemSharp.FrameworkAbstraction;
using AtemSharp.Lib;
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
    /// mixer a deterministic point at which to diff. Reads go here, never to switcher.State.
    AtemState State { get; }

    bool Connected { get; }

    IObservable<bool> WhenConnectionChanged { get; }

    IObservable<Unit> WhenStateChanged { get; }

    IEnumerable<Macro> Macros { get; }

    Task StartupAsync(CancellationToken cancellationToken);

    Task SendAsync(SerializedCommand command);
}

/// Taps the received command stream on its way to the switcher. AtemSharp offers no state change
/// notification of any kind, and no way to reach the client an AtemSwitcher built for itself, so the
/// stream is intercepted by decorating IServices before the switcher is created.
internal sealed class ObservingAtemClient : IAtemClient
{
    private readonly IAtemClient _inner;
    private readonly BufferBlock<IDeserializedCommand> _forwarded = new();
    private readonly IDisposable _link;

    public ObservingAtemClient(IAtemClient inner, Action<IDeserializedCommand> tap)
    {
        _inner = inner;
        _link = inner.ReceivedCommands.LinkTo(new ActionBlock<IDeserializedCommand>(command =>
        {
            tap(command);
            _forwarded.Post(command);
        }));
    }

    public IReceivableSourceBlock<IDeserializedCommand> ReceivedCommands => _forwarded;

    public Task ConnectAsync(string address, int port) => _inner.ConnectAsync(address, port);

    public Task DisconnectAsync() => _inner.DisconnectAsync();

    public Task SendCommandAsync(SerializedCommand command) => _inner.SendCommandAsync(command);

    public Task SendCommandsAsync(IEnumerable<SerializedCommand> commands) => _inner.SendCommandsAsync(commands);

    public async ValueTask DisposeAsync()
    {
        _link.Dispose();
        _forwarded.Complete();
        await _inner.DisposeAsync().ConfigureAwait(false);
    }
}

internal sealed class ObservingServices(IServices inner, Action<IDeserializedCommand> tap) : IServices
{
    public ITimeProvider TimeProvider => inner.TimeProvider;

    public IAtemClient CreateAtemClient() => new ObservingAtemClient(inner.CreateAtemClient(), tap);

    public IAtemProtocol CreateAtemProtocol() => inner.CreateAtemProtocol();

    public ICommandParser CreateCommandParser() => inner.CreateCommandParser();

    public IPacketBuilder CreatePacketBuilder() => inner.CreatePacketBuilder();

    public IUdpClient CreateUdpClient() => inner.CreateUdpClient();

    public IActionLoop StartActionLoop(Func<CancellationToken, Task> loopedAction, string name) =>
        inner.StartActionLoop(loopedAction, name);
}

internal sealed class AtemConnection : IAtemConnection, IAsyncDisposable
{
    private const int AtemPort = 9910;

    private readonly string _ip;
    private readonly ILogger _logger;
    private readonly IAtemSwitcher _switcher;
    private readonly BehaviorSubject<bool> _connected = new(false);
    private readonly Subject<Unit> _stateChanged = new();
    private Task? _startup;

    public AtemConnection(string ip, ILogger logger, IServices services)
    {
        _ip = ip;
        _logger = logger;
        _switcher = new Library(new ObservingServices(services, Apply)).CreateAtemSwitcher(ip, AtemPort);
    }

    public AtemState State { get; } = new();

    public bool Connected => _connected.Value;

    public IObservable<bool> WhenConnectionChanged => _connected;

    public IObservable<Unit> WhenStateChanged => _stateChanged;

    /// Running a macro needs a Macro instance carrying the right id, and Macro.Id has an internal
    /// setter, so the switcher's own macro system is the only way to reach one.
    public IEnumerable<Macro> Macros => _switcher.Macros;

    public Task StartupAsync(CancellationToken cancellationToken) => _startup ??= ConnectAsync(cancellationToken);

    public async Task SendAsync(SerializedCommand command)
    {
        try
        {
            await _switcher.SendCommandAsync(command).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Error($"sending {command.GetType().Name} failed - {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _switcher.DisconnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Error($"disconnect failed - {ex.Message}");
        }

        await _switcher.DisposeAsync().ConfigureAwait(false);
        _connected.OnNext(false);
        _connected.Dispose();
        _stateChanged.Dispose();
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await _switcher.ConnectAsync(cancellationToken).ConfigureAwait(false);
        Log("connected");
        _connected.OnNext(true);
    }

    // The switcher replays its whole state on connect, so this also seeds State.
    private void Apply(IDeserializedCommand command)
    {
        try
        {
            command.ApplyToState(State);
            _stateChanged.OnNext(Unit.Default);
        }
        catch (Exception ex)
        {
            Error($"applying {command.GetType().Name} failed - {ex.Message}");
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
