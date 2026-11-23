using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cgf.CameraControl.App.Configuration;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;
using Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;
using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;
using Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Core.VideoMixer.Passthrough;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.App.Editing;

/// Which fields each configuration record puts on the form, and what a newly added entry starts as.
///
/// Every field reads and writes a property of the record it was built for, so a property that is
/// renamed or retyped fails the build here rather than leaving a field that edits nothing.
internal static class EntrySchema
{
    public static IReadOnlyList<string> CameraTypes { get; } =
    [
        ViscaOverIpCameraConfiguration.TypeName,
        WebsocketPtzLancCameraConfiguration.TypeName,
        SignalrPtzLancCameraConfiguration.TypeName,
    ];

    public static IReadOnlyList<string> MixerTypes { get; } = [AtemConfiguration.TypeName, PassthroughConfiguration.TypeName];

    public static IReadOnlyList<string> InterfaceTypes { get; } = [GamepadConfiguration.TypeName, KeyboardConfiguration.TypeName];

    public static IReadOnlyList<Field> Fields(ILocalizer strings, EntryConfiguration entry, IReadOnlyList<string> pads) =>
        entry switch
        {
            ViscaOverIpCameraConfiguration visca =>
            [
                new TextField(strings, "ip", "edit.ip", required: true, () => visca.Ip, value => visca.Ip = value ?? string.Empty),
                // An empty box is the protocol's own port, which is what a file without one means.
                new NumberField(strings, "port", "edit.port", required: false, minimum: 1,
                    () => visca.Port, value => visca.Port = value,
                    "edit.portDefault", maximum: 65535),
                new SwitchField(strings, "panTiltInvert", "edit.panTiltInvert",
                    () => visca.PanTiltInvert ?? false, value => visca.PanTiltInvert = value),
                new PercentField(strings, "panTiltSpeedPercent", "edit.panTiltSpeedPercent",
                    () => visca.PanTiltSpeedPercent ?? 100, value => visca.PanTiltSpeedPercent = value),
                Choice(strings, "tallyMode", "edit.tallyMode", ConfigurationContext.Default.ViscaTallyMode,
                    () => visca.TallyMode ?? ViscaTallyMode.None, value => visca.TallyMode = value),
            ],
            WebsocketPtzLancCameraConfiguration websocket =>
            [
                new TextField(strings, "ip", "edit.ip", required: true, () => websocket.Ip, value => websocket.Ip = value ?? string.Empty),
                new SwitchField(strings, "panTiltInvert", "edit.panTiltInvert",
                    () => websocket.PanTiltInvert ?? false, value => websocket.PanTiltInvert = value),
                new PercentField(strings, "panTiltSpeedPercent", "edit.panTiltSpeedPercent",
                    () => websocket.PanTiltSpeedPercent ?? 100, value => websocket.PanTiltSpeedPercent = value),
                new SwitchField(strings, "showTallyLight", "edit.showTallyLight",
                    () => websocket.ShowTallyLight ?? true, value => websocket.ShowTallyLight = value),
            ],
            SignalrPtzLancCameraConfiguration signalr =>
            [
                new TextField(strings, "connectionUrl", "edit.connectionUrl", required: true,
                    () => signalr.ConnectionUrl, value => signalr.ConnectionUrl = value ?? string.Empty)
                {
                    Check = value => Validate.IsHttpUrl(value) ? null : strings.Text("edit.notAUrl"),
                },
                new TextField(strings, "connectionPort", "edit.connectionPort", required: true,
                    () => signalr.ConnectionPort, value => signalr.ConnectionPort = value ?? string.Empty),
                new SwitchField(strings, "panTiltInvert", "edit.panTiltInvert",
                    () => signalr.PanTiltInvert ?? false, value => signalr.PanTiltInvert = value),
                new PercentField(strings, "panTiltSpeedPercent", "edit.panTiltSpeedPercent",
                    () => signalr.PanTiltSpeedPercent ?? 100, value => signalr.PanTiltSpeedPercent = value),
            ],
            AtemConfiguration atem =>
            [
                new TextField(strings, "ip", "edit.ip", required: true, () => atem.Ip, value => atem.Ip = value ?? string.Empty),
                new NumberField(strings, "mixEffectBlock", "edit.mixEffectBlock", required: true, minimum: 0,
                    () => atem.MixEffectBlock, value => atem.MixEffectBlock = value ?? 0),
                Choice(strings, "tallyMode", "edit.mixerTallyMode", ConfigurationContext.Default.MixerTallyMode,
                    () => atem.TallyMode ?? MixerTallyMode.PreviewProgram, value => atem.TallyMode = value,
                    optionLabelPrefix: "edit.mixerTally"),
            ],
            PassthroughConfiguration passthrough =>
            [
                Choice(strings, "tallyMode", "edit.mixerTallyMode", ConfigurationContext.Default.MixerTallyMode,
                    () => passthrough.TallyMode ?? MixerTallyMode.PreviewProgram, value => passthrough.TallyMode = value,
                    optionLabelPrefix: "edit.mixerTally"),
            ],
            GamepadConfiguration pad => Gamepad(strings, pad, pads),
            KeyboardConfiguration keyboard =>
            [
                VideoMixer(strings, keyboard),
                CameraMap(strings, keyboard),
                EnableChangingProgram(strings, keyboard),
            ],
            _ => [],
        };

    public static IReadOnlyList<string> TypesOf(EntryKind kind) => kind switch
    {
        EntryKind.Camera => CameraTypes,
        EntryKind.Mixer => MixerTypes,
        _ => InterfaceTypes,
    };

    /// A new entry of the type, as a draft of its own section.
    public static EntryDraft NewDraft(ILocalizer strings, EntryKind kind, string type, int instance, IReadOnlyList<string> pads) =>
        kind switch
        {
            EntryKind.Camera => EntryDraft.Camera(strings, NewCamera(type, instance), pads),
            EntryKind.Mixer => EntryDraft.Mixer(strings, NewMixer(type, instance), pads),
            _ => EntryDraft.Interface(strings, NewInterface(type, instance), pads),
        };

    private static CameraConfiguration NewCamera(string type, int instance) => type switch
    {
        ViscaOverIpCameraConfiguration.TypeName => new ViscaOverIpCameraConfiguration { Instance = instance },
        WebsocketPtzLancCameraConfiguration.TypeName => new WebsocketPtzLancCameraConfiguration { Instance = instance },
        SignalrPtzLancCameraConfiguration.TypeName => new SignalrPtzLancCameraConfiguration { Instance = instance },
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "not a camera type"),
    };

    private static VideoMixerConfiguration NewMixer(string type, int instance) => type switch
    {
        AtemConfiguration.TypeName => new AtemConfiguration { Instance = instance },
        PassthroughConfiguration.TypeName => new PassthroughConfiguration { Instance = instance },
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "not a video mixer type"),
    };

    /// An interface without a `connectionChange` or a `cameraMap` is rejected whole, so a new one is
    /// born holding the empty versions of both, which its record starts with, and the first mixer.
    private static InterfaceConfiguration NewInterface(string type, int instance) => type switch
    {
        GamepadConfiguration.TypeName => new GamepadConfiguration { Instance = instance, VideoMixer = 1 },
        KeyboardConfiguration.TypeName => new KeyboardConfiguration { Instance = instance, VideoMixer = 1 },
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "not an interface type"),
    };

    private static IReadOnlyList<Field> Gamepad(ILocalizer strings, GamepadConfiguration pad, IReadOnlyList<string> pads)
    {
        var names = ConfigurationContext.Default.GamepadBackend;
        var backend = new ChoiceField(
            strings,
            "backend",
            "edit.gamepadBackend",
            pad.SupportsHid ? Names(names) : [Name(GamepadBackend.Sdl, names)],
            () => Name(pad.Backend ?? GamepadBackend.Sdl, names),
            value => pad.Backend = Parse(value, names));
        var hid = Name(GamepadBackend.Hid, names);
        return
        [
            backend,
            VideoMixer(strings, pad),
            CameraMap(strings, pad),
            EnableChangingProgram(strings, pad),
            new TextField(strings, "serialNumber", "edit.serialNumber", required: false,
                () => pad.SerialNumber, value => pad.SerialNumber = value, "edit.anyPad", pads)
            {
                RequiredWhen = () => string.Equals(backend.Value, hid, StringComparison.OrdinalIgnoreCase),
                // The loader refuses a serial with no letter or digit in it, as it would refuse none.
                Check = serial =>
                    serial.Any(char.IsLetterOrDigit) || !string.Equals(backend.Value, hid, StringComparison.OrdinalIgnoreCase)
                        ? null
                        : strings.Text("edit.required"),
            },
            new FractionField(strings, "deadzone", "edit.deadzone", maximum: 1,
                () => pad.Deadzone ?? GamepadConfiguration.DefaultDeadzone, value => pad.Deadzone = value),
            new SwitchField(strings, "rumble", "edit.rumble", () => pad.Rumble ?? true, value => pad.Rumble = value),
        ];
    }

    private static NumberField VideoMixer(ILocalizer strings, ControlInterfaceConfiguration control) =>
        new(strings, "videoMixer", "edit.videoMixer", required: true, minimum: 1,
            () => control.VideoMixer, value => control.VideoMixer = value ?? 0);

    private static MapField CameraMap(ILocalizer strings, ControlInterfaceConfiguration control) =>
        new(strings, "cameraMap", "edit.cameraMap", "edit.input", "edit.camera",
            () => control.CameraMap, value => control.CameraMap = value);

    private static SwitchField EnableChangingProgram(ILocalizer strings, ControlInterfaceConfiguration control) =>
        new(strings, "enableChangingProgram", "edit.enableChangingProgram",
            () => control.EnableChangingProgram ?? true, value => control.EnableChangingProgram = value);

    private static ChoiceField Choice<T>(
        ILocalizer strings,
        string key,
        string label,
        JsonTypeInfo<T> names,
        Func<T> read,
        Action<T> write,
        string? optionLabelPrefix = null)
        where T : struct, Enum =>
        new(strings, key, label, Names(names), () => Name(read(), names), value => write(Parse(value, names)), optionLabelPrefix);

    /// An enum's values as the file spells them, in declaration order. The spelling is the one its
    /// JSON contract writes, so the form cannot drift from the file.
    private static IReadOnlyList<string> Names<T>(JsonTypeInfo<T> names)
        where T : struct, Enum => [.. Enum.GetValues<T>().Select(value => Name(value, names))];

    private static string Name<T>(T value, JsonTypeInfo<T> names)
        where T : struct, Enum =>
        JsonSerializer.Deserialize(JsonSerializer.Serialize(value, names), ConfigurationContext.Default.String)!;

    private static T Parse<T>(string name, JsonTypeInfo<T> names)
        where T : struct, Enum =>
        Enum.GetValues<T>().First(value => string.Equals(Name(value, names), name, StringComparison.Ordinal));
}
