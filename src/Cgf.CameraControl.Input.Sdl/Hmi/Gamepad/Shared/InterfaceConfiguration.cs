using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration.Converters;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

/// What a pad and a keyboard interface have in common: the mixer they drive, the cameras behind its
/// inputs, how a direction changes the selection, and the functions either kind can bind.
///
/// The two kinds carry separate schemas rather than one schema with unused halves. A `keys` block on
/// a pad, or a `deadzone` on a keyboard, is a binding that will never fire, and an operator wants to
/// be told about it rather than left wondering why the key does nothing.
/// On the `get; set;` and [JsonRequired] conventions, see WebsocketPtzLancCameraConfiguration.
public abstract record InterfaceConfiguration
{
    /// Every entry carries these two, and an unmapped member is an error, so leaving them off here
    /// would make every interface in the file fail on its own type.
    [JsonRequired]
    public string Type { get; set; } = null!;

    [JsonRequired]
    [JsonConverter(typeof(PositiveIntConverter))]
    public int Instance { get; set; }

    [JsonRequired]
    [JsonConverter(typeof(PositiveIntConverter))]
    public int VideoMixer { get; set; }

    [JsonRequired]
    public ConnectionChangeConfiguration ConnectionChange { get; set; } = null!;

    /// Maps the mixer's input channel to a camera instance in the configuration.
    [JsonRequired]
    public IReadOnlyDictionary<int, int> CameraMap { get; set; } = null!;

    /// Named here and bound by name below, so one function reaches a face button and a key without
    /// being written twice, and so the name is available to write on the button that runs it.
    public IReadOnlyDictionary<string, SpecialFunctionConfiguration> Functions { get; set; } =
        new Dictionary<string, SpecialFunctionConfiguration>();

    public bool EnableChangingProgram { get; set; } = true;
}

/// A pad has four face buttons and no more, so the way past four is the modifier sets.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GamepadConfiguration : InterfaceConfiguration
{
    /// When set, only the pad reporting this serial is claimed, and no other pad is ever substituted.
    /// When absent, the first pad not already claimed by another interface is used.
    public string? SerialNumber { get; set; }

    /// Stick travel ignored around centre. The HID path this replaces had a flat spot built into its
    /// interpolation table; SDL reports raw axes, so without this the cameras drift.
    [JsonConverter(typeof(DeadzoneConverter))]
    public double Deadzone { get; set; } = 0.05;

    public bool Rumble { get; set; } = true;

    public PadBindings Pad { get; set; } = new();
}

/// Which named function each face button runs, per modifier.
public sealed record PadBindings
{
    public IReadOnlyDictionary<ButtonDirection, string> Default { get; set; } =
        new Dictionary<ButtonDirection, string>();

    public IReadOnlyDictionary<ButtonDirection, string>? Alt { get; set; }

    public IReadOnlyDictionary<ButtonDirection, string>? AltLower { get; set; }
}

/// A keyboard and mouse surface. Nothing is bound unless it is named here, and nothing named here is
/// left off the screen, so the panel and the file say the same thing.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record KeyboardConfiguration : InterfaceConfiguration
{
    [JsonRequired]
    public KeyBindings Keys { get; set; } = null!;
}

/// Key names are Avalonia's, so `D1` rather than `1` and `LeftShift` rather than `Shift`. They are
/// matched without regard to case, and a name that is not one of them is reported against the entry
/// that carries it rather than silently doing nothing.
public sealed record KeyBindings
{
    public PanKeys? Pan { get; set; }

    public TiltKeys? Tilt { get; set; }

    public ZoomKeys? Zoom { get; set; }

    public FocusKeys? Focus { get; set; }

    /// Keys that step the selection through the connectionChange scheme, as the direction pad does.
    public IReadOnlyDictionary<ButtonDirection, string>? ConnectionChange { get; set; }

    /// Keys that select a mixer input outright, which is what a pad has no room for.
    public IReadOnlyDictionary<string, int>? Input { get; set; }

    /// Key to the name of a function in `functions`.
    public IReadOnlyDictionary<string, string>? Function { get; set; }

    public string? Cut { get; set; }

    public string? Auto { get; set; }

    public string? Alt { get; set; }

    public string? AltLower { get; set; }
}

/// The four axis pairs are named for the direction the camera moves rather than for a sign, because
/// a file that says `"tilt": { "up": "W" }` needs no table to read.
public sealed record PanKeys
{
    public string? Left { get; set; }

    public string? Right { get; set; }
}

public sealed record TiltKeys
{
    public string? Up { get; set; }

    public string? Down { get; set; }
}

public sealed record ZoomKeys
{
    public string? In { get; set; }

    public string? Out { get; set; }
}

public sealed record FocusKeys
{
    public string? Far { get; set; }

    public string? Near { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(GamepadConfiguration))]
[JsonSerializable(typeof(KeyboardConfiguration))]
public sealed partial class InterfaceConfigurationContext : JsonSerializerContext;
