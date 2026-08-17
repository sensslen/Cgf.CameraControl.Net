using System.Text.Json.Serialization;

namespace Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;

/// speedCameraStateSchema, the payload exchanged with the camera controller firmware.
public readonly record struct WebsocketPtzLancCameraSpeedState
{
    public int Pan { get; init; }

    public int Tilt { get; init; }

    public int Zoom { get; init; }

    public int Red { get; init; }

    public int Green { get; init; }

    /// The controller echoes its state back and the next send waits for that echo. Tally is only
    /// part of the comparison when this camera drives a tally light, matching the TypeScript
    /// implementation, which otherwise never settles on a controller that reports its own colours.
    public bool Matches(WebsocketPtzLancCameraSpeedState other, bool includeTally)
    {
        var moves = Pan == other.Pan && Tilt == other.Tilt && Zoom == other.Zoom;
        return includeTally ? moves && Red == other.Red && Green == other.Green : moves;
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WebsocketPtzLancCameraSpeedState))]
public sealed partial class WebsocketPtzLancCameraStateContext : JsonSerializerContext;
