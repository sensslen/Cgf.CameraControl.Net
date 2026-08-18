using System.Reactive.Disposables;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Hmi;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.SpecialFunctions;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

public sealed class Gamepad : IHmi
{
    private readonly GamepadConfiguration _config;
    private readonly IGamepadDevice _device;
    private readonly ILogger _logger;
    private readonly IVideoMixer? _mixer;
    private readonly Dictionary<int, ICameraConnection> _cameras = [];
    private readonly IConnectionChange _connectionChange;
    private readonly Dictionary<ButtonDirection, ISpecialFunction> _default = [];
    private readonly Dictionary<ButtonDirection, ISpecialFunction> _alt = [];
    private readonly Dictionary<ButtonDirection, ISpecialFunction> _altLower = [];
    private readonly CompositeDisposable _subscriptions = [];
    private readonly CancellationTokenSource _stopping = new();

    private AltKeyConfiguration _modifiers;
    private ICameraConnection? _selectedPreviewCamera;
    private ICameraConnection? _selectedOnAirCamera;
    private int _selectedInput = -1;

    public Gamepad(
        GamepadConfiguration config,
        IGamepadDevice device,
        IVideoMixer? mixer,
        Func<int, ICameraConnection?> resolveCamera,
        ILogger logger)
    {
        _config = config;
        _device = device;
        _logger = logger;
        _mixer = mixer;
        _connectionChange = ConnectionChangeFactory.Get(config.ConnectionChange);

        foreach (var (input, cameraInstance) in config.CameraMap)
        {
            if (resolveCamera(cameraInstance) is { } camera)
            {
                _cameras[input] = camera;
            }
        }

        Fill(_default, config.SpecialFunction.Default);
        Fill(_alt, config.SpecialFunction.Alt);
        Fill(_altLower, config.SpecialFunction.AltLower);

        _subscriptions.Add(device.Modifiers.Subscribe(modifiers => _modifiers = modifiers));
        _subscriptions.Add(device.LeftStick.Subscribe(OnLeftStick));
        _subscriptions.Add(device.RightStick.Subscribe(OnRightStick));
        _subscriptions.Add(device.ConnectionChangeRequested.Subscribe(ChangeConnection));
        _subscriptions.Add(device.SpecialFunctionRequested.Subscribe(RunSpecialFunction));
        _subscriptions.Add(device.TransitionRequested.Subscribe(RunTransition));

        if (mixer is not null)
        {
            _subscriptions.Add(mixer.WhenPreviewChanged.Subscribe(OnMixerPreviewChanged));
            _subscriptions.Add(mixer.WhenProgramChanged.Subscribe(OnMixerProgramChanged));
        }
    }

    public string Description => _device.Description;

    public IObservable<bool> WhenConnectedChanged => _device.WhenConnectedChanged;

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        _subscriptions.Dispose();
        _stopping.Dispose();
        await _device.DisposeAsync().ConfigureAwait(false);
    }

    private static void Fill(
        Dictionary<ButtonDirection, ISpecialFunction> target,
        IReadOnlyDictionary<ButtonDirection, SpecialFunctionConfiguration>? source)
    {
        if (source is null)
        {
            return;
        }

        foreach (var (direction, config) in source)
        {
            target[direction] = SpecialFunctionFactory.Get(config);
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
            _mixer?.ChangeInput(next);
        }
    }

    private void RunSpecialFunction(ButtonDirection direction)
    {
        if (_mixer is null)
        {
            return;
        }

        // A modifier only overrides the default when it has something bound for that button.
        var bound = _modifiers switch
        {
            { Alt: true } when _alt.TryGetValue(direction, out var alt) => alt,
            { AltLower: true } when _altLower.TryGetValue(direction, out var altLower) => altLower,
            _ => _default.GetValueOrDefault(direction),
        };

        if (bound is null)
        {
            return;
        }

        _ = RunSafelyAsync(bound, _mixer);
    }

    private async Task RunSafelyAsync(ISpecialFunction function, IVideoMixer mixer)
    {
        try
        {
            await function.RunAsync(mixer, _stopping.Token).ConfigureAwait(false);
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
                _mixer?.Cut();
                break;
            case MixerTransition.Auto:
                _mixer?.Auto();
                break;
        }
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
    }

    private static string OnAirSuffix(bool onAir) => onAir ? " - OnAir" : string.Empty;

    private void Log(string message) => _logger.Log($"Gamepad:{message}");

    private void LogError(string message) => _logger.Error($"Gamepad:{message}");
}
