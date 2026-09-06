using System.Globalization;
using System.Text.Json.Nodes;

namespace Cgf.CameraControl.App.Editing;

/// Which fields each configuration type puts on the form, and what a newly added entry starts as.
///
/// The shapes are written out here rather than read off the configuration records, because reading
/// them off would mean reflecting over types the published binary has trimmed. The records stay the
/// authority: the loader runs over whatever this writes, and reports anything this got wrong against
/// the entry that carries it.
internal static class EntrySchema
{
    public static IReadOnlyList<string> CameraTypes { get; } = ["viscaoverip", "Websocket.PtzLanc", "Signalr.PtzLanc"];

    public static IReadOnlyList<string> MixerTypes { get; } = ["blackmagicdesign/atem", "passthrough/default"];

    public static IReadOnlyList<string> InterfaceTypes { get; } = ["gamepad", "keyboard"];

    /// A type nothing here knows is still shown and still saved, from the JSON it arrived with. The
    /// configuration is older than this editor and will outlive it.
    public static IReadOnlyList<Field> Fields(
        string type,
        JsonObject entry,
        IReadOnlyList<string> pads) => Kind(type) switch
        {
            "viscaoverip" =>
            [
                new TextField(entry, "ip", "edit.ip", required: true),
                new NumberField(entry, "port", "edit.port", required: false, minimum: 1, "edit.portDefault"),
                new SwitchField(entry, "panTiltInvert", "edit.panTiltInvert", fallback: false),
                new ChoiceField(entry, "tallyMode", "edit.tallyMode", ["none", "avonic", "ptzoptics"], "none"),
            ],
            "websocket.ptzlanc" =>
            [
                new TextField(entry, "ip", "edit.ip", required: true),
                new SwitchField(entry, "panTiltInvert", "edit.panTiltInvert", fallback: false),
                new SwitchField(entry, "showTallyLight", "edit.showTallyLight", fallback: true),
            ],
            "signalr.ptzlanc" =>
            [
                new TextField(entry, "connectionUrl", "edit.connectionUrl", required: true),
                new TextField(entry, "connectionPort", "edit.connectionPort", required: true),
                new SwitchField(entry, "panTiltInvert", "edit.panTiltInvert", fallback: false),
            ],
            "blackmagicdesign/atem" =>
            [
                new TextField(entry, "ip", "edit.ip", required: true),
                new NumberField(entry, "mixEffectBlock", "edit.mixEffectBlock", required: true, minimum: 0),
            ],
            "gamepad" =>
            [
                new NumberField(entry, "videoMixer", "edit.videoMixer", required: true, minimum: 1),
                new MapField(entry, "cameraMap", "edit.cameraMap", "edit.input", "edit.camera"),
                new SwitchField(entry, "enableChangingProgram", "edit.enableChangingProgram", fallback: true),
                new TextField(entry, "serialNumber", "edit.serialNumber", required: false, "edit.anyPad", pads),
                new FractionField(entry, "deadzone", "edit.deadzone", fallback: 0.05, maximum: 1),
                new SwitchField(entry, "rumble", "edit.rumble", fallback: true),
            ],
            "keyboard" =>
            [
                new NumberField(entry, "videoMixer", "edit.videoMixer", required: true, minimum: 1),
                new MapField(entry, "cameraMap", "edit.cameraMap", "edit.input", "edit.camera"),
                new SwitchField(entry, "enableChangingProgram", "edit.enableChangingProgram", fallback: true),
            ],
            _ => [],
        };

    /// What a newly added entry has to carry before the loader will take it. An interface without a
    /// `connectionChange` or a `cameraMap` is rejected whole, so a new one is born holding the empty
    /// versions of both rather than being saved into a file that will not open.
    public static JsonObject NewEntry(string type, int instance)
    {
        var entry = new JsonObject
        {
            ["type"] = JsonValue.Create(type),
            ["instance"] = JsonValue.Create(instance),
        };

        if (Kind(type) is "gamepad" or "keyboard")
        {
            entry["videoMixer"] = JsonValue.Create(1);
            entry["connectionChange"] = new JsonObject
            {
                ["type"] = JsonValue.Create("direct"),
                ["default"] = new JsonObject(),
            };
            entry["cameraMap"] = new JsonObject();
        }

        if (Kind(type) is "keyboard")
        {
            entry["keys"] = new JsonObject();
        }

        return entry;
    }

    /// The three `logitech/*` strings still load as pads, so they take the pad's form rather than
    /// falling through as a type nothing knows.
    private static string Kind(string type) => type.ToLower(CultureInfo.InvariantCulture) switch
    {
        "logitech/f310" or "logitech/f710" or "logitech/rumblepad2" => "gamepad",
        var known => known,
    };
}
