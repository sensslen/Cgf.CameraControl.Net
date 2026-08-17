using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration.Converters;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

/// gamepadConfigurationSchema, with the device selection reworked for SDL.
///
/// SDL normalises every pad to one button and axis layout, so the controller model no longer selects
/// any behaviour and logitech/F310, logitech/F710 and logitech/Rumblepad2 all build the same thing.
/// What does change is how a pad is chosen, and what the analogue inputs need on top of raw axes.
/// On the `get; set;` and [JsonRequired] conventions, see WebsocketPtzLancCameraConfiguration. Getting
/// them wrong here would leave Deadzone at zero and Rumble off.
public sealed record GamepadConfiguration
{
    [JsonRequired]
    [JsonConverter(typeof(PositiveIntConverter))]
    public int VideoMixer { get; set; }

    [JsonRequired]
    public ConnectionChangeConfiguration ConnectionChange { get; set; } = null!;

    [JsonRequired]
    public SpecialFunctionSet SpecialFunction { get; set; } = null!;

    /// Maps the mixer's input channel to a camera instance in the configuration.
    [JsonRequired]
    public IReadOnlyDictionary<int, int> CameraMap { get; set; } = null!;

    public bool EnableChangingProgram { get; set; } = true;

    /// When set, only the pad reporting this serial is claimed, and no other pad is ever substituted.
    /// When absent, the first pad not already claimed by another interface is used.
    public string? SerialNumber { get; set; }

    /// Stick travel ignored around centre. The HID path this replaces had a flat spot built into its
    /// interpolation table; SDL reports raw axes, so without this the cameras drift.
    public double Deadzone { get; set; } = 0.05;

    public bool Rumble { get; set; } = true;
}

public sealed record SpecialFunctionSet
{
    [JsonRequired]
    public IReadOnlyDictionary<ButtonDirection, SpecialFunctionConfiguration> Default { get; set; } = null!;

    public IReadOnlyDictionary<ButtonDirection, SpecialFunctionConfiguration>? Alt { get; set; }

    public IReadOnlyDictionary<ButtonDirection, SpecialFunctionConfiguration>? AltLower { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(GamepadConfiguration))]
public sealed partial class GamepadConfigurationContext : JsonSerializerContext;
