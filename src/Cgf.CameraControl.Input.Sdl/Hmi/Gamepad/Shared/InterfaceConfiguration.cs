using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Configuration.Converters;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

/// What a pad and a keyboard interface have in common: the mixer they drive, the cameras behind its
/// inputs, how a direction changes the selection, and the functions either kind can bind.
///
/// The two kinds carry separate schemas rather than one schema with unused halves. A `keys` block on
/// a pad, or a `deadzone` on a keyboard, is a binding that will never fire, and an operator wants to
/// be told about it rather than left wondering why the key does nothing.
/// On the `get; set;` and [JsonRequired] conventions, see EntryConfiguration.
public abstract record ControlInterfaceConfiguration(string Type) : InterfaceConfiguration(Type)
{
    [JsonRequired]
    [JsonConverter(typeof(PositiveIntConverter))]
    public int VideoMixer { get; set; }

    [JsonRequired]
    public ConnectionChangeConfiguration ConnectionChange { get; set; } =
        new DirectConnectionChangeConfiguration();

    /// Maps the mixer's input channel to a camera instance in the configuration.
    [JsonRequired]
    public IReadOnlyDictionary<int, int> CameraMap { get; set; } = new Dictionary<int, int>();

    /// Named here and bound by name below, so one function reaches a face button and a key without
    /// being written twice, and so the name is available to write on the button that runs it.
    public IReadOnlyDictionary<string, SpecialFunctionConfiguration>? Functions { get; set; }

    public bool? EnableChangingProgram { get; set; }
}

/// A pad has four face buttons and no more, so the way past four is the modifier sets.
public record GamepadConfiguration : ControlInterfaceConfiguration
{
    public const string TypeName = "gamepad";

    public const double DefaultDeadzone = 0.05;

    public GamepadConfiguration()
        : this(TypeName)
    {
    }

    protected GamepadConfiguration(string type)
        : base(type)
    {
    }

    /// The HID decoder reads only the F310's reports.
    [JsonIgnore]
    public virtual bool SupportsHid => true;

    public GamepadBackend? Backend { get; set; }

    /// When set, only the pad reporting this serial is claimed, and no other pad is ever substituted.
    /// When absent, the first pad not already claimed by another interface is used.
    public string? SerialNumber { get; set; }

    /// Stick travel ignored around centre. The HID path this replaces had a flat spot built into its
    /// interpolation table; SDL reports raw axes, so without this the cameras drift.
    [JsonConverter(typeof(DeadzoneConverter))]
    public double? Deadzone { get; set; }

    public bool? Rumble { get; set; }

    public PadBindings? Pad { get; set; }
}

/// The names pads were configured by before `gamepad`. Each is its own record because a type string
/// is what the file is written back with, and one record can be written back as only one of them.
public sealed record LogitechF310Configuration() : GamepadConfiguration(TypeName)
{
    public new const string TypeName = "logitech/F310";
}

public sealed record LogitechF710Configuration() : GamepadConfiguration(TypeName)
{
    public new const string TypeName = "logitech/F710";

    public override bool SupportsHid => false;
}

public sealed record LogitechRumblepad2Configuration() : GamepadConfiguration(TypeName)
{
    public new const string TypeName = "logitech/Rumblepad2";

    public override bool SupportsHid => false;
}

public enum GamepadBackend
{
    [JsonStringEnumMemberName("sdl")]
    Sdl,
    [JsonStringEnumMemberName("hid")]
    Hid,
}

/// Which named function each face button runs, per modifier.
public sealed record PadBindings
{
    public IReadOnlyDictionary<ButtonDirection, string>? Default { get; set; }

    public IReadOnlyDictionary<ButtonDirection, string>? Alt { get; set; }

    public IReadOnlyDictionary<ButtonDirection, string>? AltLower { get; set; }
}

/// A keyboard and mouse surface. Nothing is bound unless it is named here, and nothing named here is
/// left off the screen, so the panel and the file say the same thing.
public sealed record KeyboardConfiguration() : ControlInterfaceConfiguration(TypeName)
{
    public const string TypeName = "keyboard";

    [JsonRequired]
    public KeyBindings Keys { get; set; } = new();
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
