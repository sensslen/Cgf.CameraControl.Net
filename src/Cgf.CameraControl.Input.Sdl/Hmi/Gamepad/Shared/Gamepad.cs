using System.Reactive.Disposables;
using System.Reactive.Linq;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Hmi;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.SpecialFunctions;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

public sealed class Gamepad : IHmi
{
    // Long enough to feel through a thumb resting on the pad, short enough not to blur into the next
    // one when an operator works a fast sequence of cuts.
    private static readonly TimeSpan TransitionPulse = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan OnAirPulse = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ConnectionLostPulse = TimeSpan.FromMilliseconds(600);

    private readonly InterfaceConfiguration _config;
    private readonly IGamepadDevice _device;
    private readonly ILogger _logger;
    private readonly IVideoMixer _mixer;
    private readonly Dictionary<int, ICameraConnection> _cameras = [];
    private readonly IConnectionChange _connectionChange;
    private readonly Dictionary<string, ISpecialFunction> _functions = [];
    private readonly Dictionary<ButtonDirection, string> _default = [];
    private readonly Dictionary<ButtonDirection, string> _alt = [];
    private readonly Dictionary<ButtonDirection, string> _altLower = [];
    private readonly CompositeDisposable _subscriptions = [];
    private readonly CancellationTokenSource _stopping = new();
    private readonly PadBindings? _pad;

    private AltKeyConfiguration _modifiers;
    private ICameraConnection? _selectedPreviewCamera;
    private ICameraConnection? _selectedOnAirCamera;
    private int _selectedInput = -1;
    private bool _mixerWasConnected;

    /// A keyboard reports the functions it runs by name and the inputs it selects outright, neither of
    /// which a pad has any way to say. They arrive as streams rather than as members on the device,
    /// so the pad path stays exactly what it was and nothing here has to ask which kind it holds.
    public Gamepad(
        InterfaceConfiguration config,
        IGamepadDevice device,
        IVideoMixer mixer,
        Func<int, ICameraConnection?> resolveCamera,
        ILogger logger,
        PadBindings? pad = null,
        IObservable<string>? functions = null,
        IObservable<int>? inputs = null)
    {
        _config = config;
        _device = device;
        _logger = logger;
        _mixer = mixer;
        _connectionChange = ConnectionChangeFactory.Get(config.ConnectionChange);
        _pad = pad;

        foreach (var (input, cameraInstance) in config.CameraMap)
        {
            if (resolveCamera(cameraInstance) is { } camera)
            {
                _cameras[input] = camera;
            }
        }

        foreach (var (name, definition) in config.Functions)
        {
            _functions[name] = SpecialFunctionFactory.Get(definition);
        }

        Bind(_default, pad?.Default);
        Bind(_alt, pad?.Alt);
        Bind(_altLower, pad?.AltLower);

        _subscriptions.Add(device.Modifiers.Subscribe(modifiers => _modifiers = modifiers));
        _subscriptions.Add(device.LeftStick.Subscribe(OnLeftStick));
        _subscriptions.Add(device.RightStick.Subscribe(OnRightStick));
        _subscriptions.Add(device.ConnectionChangeRequested.Subscribe(ChangeConnection));
        _subscriptions.Add(device.SpecialFunctionRequested.Subscribe(OnFaceButton));
        if (functions is not null)
        {
            _subscriptions.Add(functions.Subscribe(RunNamed));
        }

        if (inputs is not null)
        {
            _subscriptions.Add(inputs.Subscribe(SelectInput));
        }

        _subscriptions.Add(device.TransitionRequested.Subscribe(RunTransition));
        _subscriptions.Add(mixer.WhenPreviewChanged.Subscribe(OnMixerPreviewChanged));
        _subscriptions.Add(mixer.WhenProgramChanged.Subscribe(OnMixerProgramChanged));
        _subscriptions.Add(mixer.WhenConnectedChanged.DistinctUntilChanged().Subscribe(OnMixerConnectionChanged));

        foreach (var camera in _cameras.Values.Distinct())
        {
            _subscriptions.Add(camera.WhenConnectedChanged
                .DistinctUntilChanged()
                .Subscribe(connected => OnCameraConnectionChanged(camera, connected)));
        }
    }

    public string Description => _device.Description;

    /// What this interface drives, so the window can group a mixer and its cameras under the
    /// interface that owns them rather than listing everything configured in one flat panel.
    public IVideoMixer Mixer => _mixer;

    public IReadOnlyCollection<ICameraConnection> Cameras => _cameras.Values.Distinct().ToList();

    /// The window reads these to draw the interface rather than to drive it. What is on the screen
    /// has to agree with what a press would actually do, so it is answered here, where the bindings,
    /// the modifiers and the current selection already are, rather than resolved a second time.
    public InterfaceConfiguration Configuration => _config;

    public IObservable<GamepadState> DrawnState => _device.State;

    public IObservable<AltKeyConfiguration> WhenModifiersChanged => _device.Modifiers;

    /// Which camera sits behind each of the mixer's inputs.
    public IReadOnlyDictionary<int, ICameraConnection> Inputs => _cameras;

    /// The function a face button would run right now, which is not the same as the one it was
    /// configured with: a held modifier picks a different set.
    public string? BoundFunction(ButtonDirection direction) => _modifiers switch
    {
        { Alt: true } when _pad?.Alt?.TryGetValue(direction, out var alt) == true => alt,
        { AltLower: true } when _pad?.AltLower?.TryGetValue(direction, out var lower) == true => lower,
        _ => _pad?.Default.GetValueOrDefault(direction),
    };

    /// The input a direction would select from where the selection stands, which for a directional
    /// scheme changes every time the selection does.
    public int? BoundInput(ButtonDirection direction) =>
        _connectionChange.Next(direction, _selectedInput, _modifiers);

    public IObservable<bool> WhenConnectedChanged => _device.WhenConnectedChanged;

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        _subscriptions.Dispose();
        _stopping.Dispose();
        await _device.DisposeAsync().ConfigureAwait(false);
    }

    private static void Bind(
        Dictionary<ButtonDirection, string> target,
        IReadOnlyDictionary<ButtonDirection, string>? source)
    {
        if (source is null)
        {
            return;
        }

        foreach (var (direction, name) in source)
        {
            target[direction] = name;
        }
    }

    private void OnLeftStick(StickPosition position)
    {
        _selectedPreviewCamera?.Pan(position.X);
        _selectedPreviewCamera?.Tilt(position.Y);
    }

    private void OnRightStick(StickPosition position)
    {
        _selectedPreviewCamera?.Zoom(position.Y);
        _selectedPreviewCamera?.Focus(position.X);
    }

    private void ChangeConnection(ButtonDirection direction)
    {
        if (_connectionChange.Next(direction, _selectedInput, _modifiers) is { } next)
        {
            _mixer.ChangeInput(next);
        }
    }

    private void OnFaceButton(ButtonDirection direction)
    {
        // A modifier only overrides the default when it has something bound for that button.
        var bound = _modifiers switch
        {
            { Alt: true } when _alt.TryGetValue(direction, out var alt) => alt,
            { AltLower: true } when _altLower.TryGetValue(direction, out var altLower) => altLower,
            _ => _default.GetValueOrDefault(direction),
        };

        if (bound is not null)
        {
            RunNamed(bound);
        }
    }

    /// The name is the operator's, taken from their file, so a name with nothing behind it is worth
    /// saying out loud rather than swallowing: the button they pressed did nothing.
    private void RunNamed(string name)
    {
        if (_functions.TryGetValue(name, out var function))
        {
            _ = RunSafelyAsync(function);
            return;
        }

        LogError($"no function named {name} is configured");
    }

    private void SelectInput(int input) => _mixer.ChangeInput(input);

    private async Task RunSafelyAsync(ISpecialFunction function)
    {
        try
        {
            await function.RunAsync(_mixer, _stopping.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogError($"special function failed - {ex.Message}");
        }
    }

    private void RunTransition(MixerTransition transition)
    {
        if (!_config.EnableChangingProgram)
        {
            return;
        }

        switch (transition)
        {
            case MixerTransition.Cut:
                _mixer.Cut();
                break;
            case MixerTransition.Auto:
                _mixer.Auto();
                break;
        }

        Rumble(0.35, TransitionPulse);
    }

    private void OnMixerPreviewChanged(PreviewChange change)
    {
        _selectedInput = change.Preview;
        var selected = _cameras.GetValueOrDefault(change.Preview);

        if (selected != _selectedPreviewCamera)
        {
            if (_selectedOnAirCamera != _selectedPreviewCamera)
            {
                _selectedPreviewCamera?.SetTally(TallyState.Off);
            }

            // Whatever the sticks were last asking for belonged to the previous camera, so it is
            // stopped rather than left running on a camera nobody is steering any more.
            _selectedPreviewCamera?.Pan(0);
            _selectedPreviewCamera?.Tilt(0);
            _selectedPreviewCamera?.Zoom(0);
            _selectedPreviewCamera?.Focus(0);
        }

        _selectedPreviewCamera = selected;

        if (selected is null)
        {
            Log($"Selected input:{change.Preview} (not a camera){OnAirSuffix(change.OnAir)}");
            return;
        }

        Log($"Selected input:{change.Preview} ({selected.ConnectionString}){OnAirSuffix(change.OnAir)}");
        if (_selectedOnAirCamera != selected)
        {
            selected.SetTally(TallyState.Preview);
        }
    }

    private void OnMixerProgramChanged(int program)
    {
        var onAir = _cameras.GetValueOrDefault(program);
        if (onAir == _selectedOnAirCamera)
        {
            return;
        }

        if (_selectedOnAirCamera != _selectedPreviewCamera)
        {
            _selectedOnAirCamera?.SetTally(TallyState.Off);
        }
        else
        {
            _selectedPreviewCamera?.SetTally(TallyState.Preview);
        }

        _selectedOnAirCamera = onAir;
        _selectedOnAirCamera?.SetTally(TallyState.Program);

        if (onAir is not null)
        {
            Rumble(0.7, OnAirPulse);
        }
    }

    private void OnMixerConnectionChanged(bool connected)
    {
        if (_mixerWasConnected && !connected)
        {
            LogError($"lost the connection to {_mixer.ConnectionString}");
            Rumble(1, ConnectionLostPulse);
        }

        _mixerWasConnected = connected;
    }

    // Only the camera under the operator's own sticks is worth interrupting them for; a camera
    // another desk is steering is not their problem.
    private void OnCameraConnectionChanged(ICameraConnection camera, bool connected)
    {
        if (connected || (camera != _selectedPreviewCamera && camera != _selectedOnAirCamera))
        {
            return;
        }

        LogError($"lost the connection to {camera.ConnectionString}");
        Rumble(1, ConnectionLostPulse);
    }

    private void Rumble(double intensity, TimeSpan duration) => _device.Rumble(intensity, duration);

    private static string OnAirSuffix(bool onAir) => onAir ? " - OnAir" : string.Empty;

    private void Log(string message) => _logger.Log($"Gamepad:{message}");

    private void LogError(string message) => _logger.Error($"Gamepad:{message}");
}
