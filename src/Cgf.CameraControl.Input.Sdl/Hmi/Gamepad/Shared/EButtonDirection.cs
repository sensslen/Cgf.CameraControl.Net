using System.Text.Json.Serialization;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

/// Named as the file spells it, which is how a dictionary keyed by direction is written back.
public enum ButtonDirection
{
    [JsonStringEnumMemberName("up")]
    Up,

    [JsonStringEnumMemberName("down")]
    Down,

    [JsonStringEnumMemberName("left")]
    Left,

    [JsonStringEnumMemberName("right")]
    Right,
}
