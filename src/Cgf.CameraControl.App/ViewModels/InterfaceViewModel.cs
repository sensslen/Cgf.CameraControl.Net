using System.Globalization;
using System.Reactive.Disposables;
using Avalonia.Input;
using Cgf.CameraControl.Core.Hmi;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using Cgf.CameraControl.App.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cgf.CameraControl.App.ViewModels;

/// An interface and what it drives. The panel is grouped this way rather than by kind because a desk
/// is read that way: this operator, this mixer, these cameras.
///
/// A pad is drawn and a keyboard is operated, which is the whole difference between the two kinds. A
/// pad interface cannot be driven from the window at all; a desk that wants both configures one of
/// each against the same mixer.
public sealed partial class InterfaceViewModel : ViewModelBase, IDisposable
{
    private static readonly ButtonDirection[] Corners =
        [ButtonDirection.Up, ButtonDirection.Down, ButtonDirection.Left, ButtonDirection.Right];

    private readonly CompositeDisposable _subscriptions = [];
    private readonly Gamepad? _gamepad;

    public InterfaceViewModel(
        int instance,
        IHmi hmi,
        ControlSurfaceViewModel? surface,
        IReadOnlyList<MixerViewModel> mixers,
        IReadOnlyList<CameraViewModel> cameras)
    {
        Instance = instance;
        Description = hmi.Description;
        Surface = surface;
        Mixers = mixers;
        Cameras = cameras;
        _gamepad = hmi as Gamepad;

        // A gamepad describes itself by the pad it is bound to, and it binds at the moment it
        // reports itself connected.
        _subscriptions.Add(hmi.WhenConnectedChanged.Bind(connected =>
        {
            IsConnected = connected;
            Description = hmi.Description;
        }));

        if (_gamepad is { Configuration: GamepadConfiguration })
        {
            IsGamepad = true;
            _subscriptions.Add(_gamepad.DrawnState.Bind(state => PadState = state));

            // The labels say what a press would do now, and a held modifier changes the answer, so
            // they are read again whenever the modifier moves rather than fixed at load.
            _subscriptions.Add(_gamepad.WhenModifiersChanged.Bind(_ => RelabelPad()));
            RelabelPad();

        }

        if (surface is not null && _gamepad?.Configuration is KeyboardConfiguration keyboard)
        {
            Build(keyboard, surface);
        }

        // Preview and program belong to the interface's own mixer, and an interface has exactly one.
        if (mixers.FirstOrDefault() is { } mixer)
        {
            Mixer = mixer;
            mixer.PropertyChanged += OnMixerChanged;
            Recolour();
        }
    }

    public int Instance { get; }

    /// Present on a keyboard interface and absent on a pad, which is what decides whether the window
    /// draws a controller or a set of controls to work.
    public ControlSurfaceViewModel? Surface { get; }

    public IReadOnlyList<MixerViewModel> Mixers { get; }

    public IReadOnlyList<CameraViewModel> Cameras { get; }

    public MixerViewModel? Mixer { get; }

    /// A keyboard interface draws a button per input and rings the live ones, so a strip repeating
    /// that says nothing new. A pad has no input buttons at all, which is what the strip is for.
    public bool ShowsSelection => IsGamepad && Mixer is not null;

    public bool IsGamepad { get; }

    /// Every function the file names, whether or not a key reaches it, because a function with no
    /// key is still a function the operator meant to have.
    public IReadOnlyList<KeyButtonViewModel> Functions { get; private set; } = [];

    /// The same inputs twice, as a vision mixer lays them out: the top row is what is live and the
    /// bottom row is what is queued. Only the preview row carries the keys, because that is the row
    /// the configuration binds.
    public IReadOnlyList<KeyButtonViewModel> ProgramInputs { get; private set; } = [];

    public IReadOnlyList<KeyButtonViewModel> PreviewInputs { get; private set; } = [];

    public KeyButtonViewModel? Cut { get; private set; }

    public KeyButtonViewModel? Auto { get; private set; }

    [ObservableProperty]
    public partial string Description { get; set; }

    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    public partial GamepadState PadState { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string?> FaceLabels { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<string?> DPadLabels { get; set; } = [];

    /// The input number and the camera behind it. An operator reading a strip wants to know which
    /// camera is live, and the number alone only says which button was pressed.
    [ObservableProperty]
    public partial string PreviewText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProgramText { get; set; } = string.Empty;

    /// The keys this interface answers to, so the window can turn a key press into the button that
    /// carries it without knowing anything about the configuration.
    public IEnumerable<KeyButtonViewModel> Buttons =>
        Functions.Concat(PreviewInputs).Concat(new[] { Cut, Auto }.OfType<KeyButtonViewModel>());

    /// The bindings that are not buttons: the axes a key holds open and the modifiers. Empty on a
    /// pad interface, which is not driven from the window at all.
    public InterfaceKeys Keys { get; private set; } = InterfaceKeys.None;

    public void Dispose()
    {
        if (Mixer is { } mixer)
        {
            mixer.PropertyChanged -= OnMixerChanged;
        }

        _subscriptions.Dispose();
    }

    private static Key? Parse(string? name) =>
        name is not null && Enum.TryParse<Key>(name, ignoreCase: true, out var key) ? key : null;

    private void Build(KeyboardConfiguration config, ControlSurfaceViewModel surface)
    {
        var byFunction = config.Keys.Function ?? new Dictionary<string, string>();
        Functions =
        [
            .. config.Functions.Keys.Order().Select(name => new KeyButtonViewModel(
                name,
                Parse(byFunction.FirstOrDefault(entry => entry.Value == name).Key),
                new RelayCommand(() => surface.Run(name)))),
        ];

        var inputs = (config.Keys.Input ?? new Dictionary<string, int>())
            .OrderBy(entry => entry.Value)
            .ToList();

        PreviewInputs =
        [
            .. inputs.Select(entry => new KeyButtonViewModel(
                entry.Value.ToString(CultureInfo.CurrentUICulture),
                Parse(entry.Key),
                new RelayCommand(() => surface.SelectInput(entry.Value)))),
        ];

        // Pressing a program button on a desk cuts to it, and this is the switch that says whether
        // this interface is allowed to put anything to air at all.
        ProgramInputs =
        [
            .. inputs.Select(entry => new KeyButtonViewModel(
                entry.Value.ToString(CultureInfo.CurrentUICulture),
                null,
                new RelayCommand(
                    () =>
                    {
                        surface.SelectInput(entry.Value);
                        surface.Transition(MixerTransition.Cut);
                    },
                    () => config.EnableChangingProgram))),
        ];

        Keys = new InterfaceKeys(
            Parse(config.Keys.Pan?.Left),
            Parse(config.Keys.Pan?.Right),
            Parse(config.Keys.Tilt?.Up),
            Parse(config.Keys.Tilt?.Down),
            Parse(config.Keys.Zoom?.In),
            Parse(config.Keys.Zoom?.Out),
            Parse(config.Keys.Focus?.Far),
            Parse(config.Keys.Focus?.Near),
            (config.Keys.ConnectionChange ?? new Dictionary<ButtonDirection, string>())
                .Select(entry => (Direction: entry.Key, Key: Parse(entry.Value)))
                .Where(entry => entry.Key is not null)
                .ToDictionary(entry => entry.Key!.Value, entry => entry.Direction));

        Cut = new KeyButtonViewModel(
            Localizer.Current.Text("mixer.cut"),
            Parse(config.Keys.Cut),
            new RelayCommand(() => surface.Transition(MixerTransition.Cut)));
        Auto = new KeyButtonViewModel(
            Localizer.Current.Text("mixer.auto"),
            Parse(config.Keys.Auto),
            new RelayCommand(() => surface.Transition(MixerTransition.Auto)));
    }

    /// A direction with nothing bound gets no label, which is what keeps the drawing honest about
    /// the buttons that do nothing.
    private void RelabelPad()
    {
        if (_gamepad is not { } pad)
        {
            return;
        }

        FaceLabels = [.. Corners.Select(pad.BoundFunction)];
        DPadLabels = [.. Corners.Select(direction => pad.BoundInput(direction) is { } input
            ? string.Format(
                CultureInfo.CurrentUICulture,
                Localizer.Current.Text("surface.inputLabel"),
                input)
            : null)];
    }

    private void OnMixerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        RelabelPad();
        Recolour();
    }

    private void Recolour()
    {
        PreviewText = Name(Mixer?.Preview);
        ProgramText = Name(Mixer?.Program);

        foreach (var button in PreviewInputs)
        {
            button.IsPreview = Mixer?.Preview == Number(button);
        }

        foreach (var button in ProgramInputs)
        {
            button.IsProgram = Mixer?.Program == Number(button);
        }
    }

    private static int Number(KeyButtonViewModel button) =>
        int.Parse(button.Label, CultureInfo.CurrentUICulture);

    /// An input with no camera behind it still gets its number, because "nothing is mapped there" is
    /// itself worth seeing on a strip that is meant to say what is live. A mixer that has not said
    /// anything yet is a different case, and a blank strip reads as one that is broken.
    private string Name(int? input)
    {
        if (input is not { } selected || selected < 0)
        {
            return Localizer.Current.Text("mixer.nothingSelected");
        }

        var number = selected.ToString(CultureInfo.CurrentUICulture);
        return _gamepad?.Inputs.GetValueOrDefault(selected) is { } camera
            ? $"{number}  {camera.ConnectionString}"
            : number;
    }
}
