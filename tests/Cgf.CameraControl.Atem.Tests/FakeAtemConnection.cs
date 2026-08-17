using System.Reactive;
using System.Reactive.Subjects;
using AtemSharp.Commands;
using AtemSharp.State;
using AtemSharp.State.Macro;
using Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;

namespace Cgf.CameraControl.Atem.Tests;

public sealed class FakeAtemConnection : IAtemConnection, IAtemConnectionFactory
{
    private readonly AtemStateDriver _driver = new();
    private readonly BehaviorSubject<bool> _connected = new(true);
    private readonly Subject<Unit> _stateChanged = new();

    public AtemState State => _driver.State;

    public bool Connected
    {
        get => _connected.Value;
        set => _connected.OnNext(value);
    }

    public IObservable<bool> WhenConnectionChanged => _connected;

    public IObservable<Unit> WhenStateChanged => _stateChanged;

    public IEnumerable<Macro> Macros { get; set; } = [];

    public List<SerializedCommand> Sent { get; } = [];

    public List<string> Released { get; } = [];

    /// Applies a payload the way a switcher would, then raises the change signal the mixer listens to.
    public void Push(Action<AtemStateDriver> change)
    {
        change(_driver);
        _stateChanged.OnNext(Unit.Default);
    }

    public IAtemConnection Get(string ip) => this;

    public Task ReleaseAsync(string ip)
    {
        Released.Add(ip);
        return Task.CompletedTask;
    }

    public Task StartupAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SendAsync(SerializedCommand command)
    {
        Sent.Add(command);
        return Task.CompletedTask;
    }
}
