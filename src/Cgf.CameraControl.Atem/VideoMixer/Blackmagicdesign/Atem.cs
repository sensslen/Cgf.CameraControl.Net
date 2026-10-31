using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using AtemSharp.Commands.Macro;
using AtemSharp.Commands.MixEffects;
using AtemSharp.Commands.MixEffects.Key;
using AtemSharp.Commands.MixEffects.Transition;
using AtemSharp.State;
using AtemSharp.State.Video.MixEffect;
using AtemSharp.State.Video.MixEffect.UpstreamKeyer;
using Cgf.CameraControl.Core.VideoMixer;

namespace Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;

public sealed class Atem : IVideoMixer
{
    private readonly AtemConfiguration _config;
    private readonly IAtemConnectionFactory _factory;
    private readonly IAtemConnection _connection;
    private readonly BehaviorSubject<PreviewChange> _preview = new(new PreviewChange(-1, false));
    private readonly BehaviorSubject<int> _program = new(-1);
    private readonly IDisposable _subscription;
    private readonly IDisposable _connectionSubscription;

    public Atem(AtemConfiguration config, IAtemConnectionFactory factory)
    {
        _config = config;
        _factory = factory;
        _connection = factory.Get(config.Ip);
        _subscription = _connection.WhenStateChanged.Subscribe(_ => UpdateActiveInputs());
        _connectionSubscription = _connection.WhenConnectionChanged.Subscribe(connected =>
        {
            if (!connected)
            {
                _preview.OnNext(new PreviewChange(-1, false));
                _program.OnNext(-1);
            }
        });
        UpdateActiveInputs();
    }

    public string ConnectionString => $"{_config.Ip}:{_config.MixEffectBlock}";

    public MixerTallyMode TallyMode => _config.TallyMode;

    public IObservable<bool> WhenConnectedChanged => _connection.WhenConnectionChanged;

    public IObservable<PreviewChange> WhenPreviewChanged => _preview;

    public IObservable<int> WhenProgramChanged => _program;

    public Task StartupAsync(CancellationToken cancellationToken) => _connection.StartupAsync(cancellationToken);

    public void Cut() => Send(me => new CutCommand(me));

    public void Auto() => Send(me => new AutoTransitionCommand(me));

    public void ChangeInput(int newInput) => Send(me => new PreviewInputCommand(me) { Source = (ushort)newInput });

    public void ToggleKey(int key) => Send(me =>
    {
        var keyer = FindKeyer(me, key);
        return keyer is null ? null : new MixEffectKeyOnAirCommand(keyer) { OnAir = !keyer.OnAir };
    });

    public void RunMacro(int macro)
    {
        if (_connection.Connected)
        {
            _connection.RunMacro(macro);
        }
    }

    public async Task<bool> IsKeySetAsync(int key, CancellationToken cancellationToken)
    {
        await AwaitMixEffectAsync(cancellationToken).ConfigureAwait(false);
        return _connection.Read(state => FindMixEffect(state) is { } me && (FindKeyer(me, key)?.OnAir ?? false));
    }

    public async Task<int> GetAuxiliarySelectionAsync(int aux, CancellationToken cancellationToken)
    {
        await AwaitMixEffectAsync(cancellationToken).ConfigureAwait(false);
        var source = _connection.Read(state => state.Video.Auxiliaries.FirstOrDefault(a => a.Id == aux)?.Source);
        return source ?? throw new InvalidOperationException($"the switcher has no auxiliary {aux}");
    }

    public async ValueTask DisposeAsync()
    {
        _subscription.Dispose();
        _connectionSubscription.Dispose();
        _preview.Dispose();
        _program.Dispose();
        await _factory.ReleaseAsync(_config.Ip).ConfigureAwait(false);
    }

    private void Send(Func<MixEffect, AtemSharp.Commands.SerializedCommand?> build)
    {
        if (!_connection.Connected
            || _connection.Read(state => FindMixEffect(state) is { } me ? build(me) : null) is not { } command)
        {
            return;
        }

        _connection.Send(command);
    }

    private void UpdateActiveInputs()
    {
        var inputs = _connection.Read(state => FindMixEffect(state) is { } me
            ? (Preview: new PreviewChange(me.PreviewInput, IsVisibleOnProgram(me, me.PreviewInput)), Program: me.ProgramInput)
            : default((PreviewChange Preview, int Program)?));
        if (inputs is not { } next)
        {
            return;
        }

        if (!_preview.Value.Equals(next.Preview))
        {
            _preview.OnNext(next.Preview);
        }

        if (_program.Value != next.Program)
        {
            _program.OnNext(next.Program);
        }
    }

    /// atem-connection answers this with listVisibleInputs('program'). AtemSharp has no equivalent,
    /// so it is derived from the block's own state: the program bus, the preview bus while a
    /// transition is running, and the fill of every upstream keyer that is on air.
    private static bool IsVisibleOnProgram(MixEffect me, int input)
    {
        if (me.ProgramInput == input)
        {
            return true;
        }

        if (me.TransitionPosition.InTransition && me.PreviewInput == input)
        {
            return true;
        }

        return me.UpstreamKeyers.Any(keyer => keyer.OnAir && keyer.FillSource == input);
    }

    // ItemCollection's indexer throws KeyNotFoundException for anything the switcher has not
    // reported yet, and state is empty until it does, so every lookup goes through a scan.
    private MixEffect? FindMixEffect(AtemState state) =>
        state.Video.MixEffects.FirstOrDefault(me => me.Id == _config.MixEffectBlock);

    private static UpstreamKeyer? FindKeyer(MixEffect me, int key) =>
        me.UpstreamKeyers.FirstOrDefault(keyer => keyer.Id == key);

    private bool HasMixEffect() => _connection.Read(state => FindMixEffect(state) is not null);

    private async Task AwaitMixEffectAsync(CancellationToken cancellationToken)
    {
        if (HasMixEffect())
        {
            return;
        }

        await _connection.WhenStateChanged
            .Where(_ => HasMixEffect())
            .FirstAsync()
            .ToTask(cancellationToken)
            .ConfigureAwait(false);

        if (!HasMixEffect())
        {
            throw new InvalidOperationException($"the switcher has no mix effect block {_config.MixEffectBlock}");
        }
    }
}
