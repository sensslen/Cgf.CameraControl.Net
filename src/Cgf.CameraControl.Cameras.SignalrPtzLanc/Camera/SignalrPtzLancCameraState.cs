using System.Text.Json.Serialization;

namespace Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;

/// The state the controller is asked to hold, as the SetState hub method takes it.
public readonly record struct SignalrPtzLancCameraState
{
    public int Pan { get; init; }

    public int Tilt { get; init; }

    public int Zoom { get; init; }

    public int Focus { get; init; }
}

/// The controller's connection list and the registration sent back to it.
public sealed record ControllerConnection(string ConnectionName, bool Connected);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SignalrPtzLancCameraState))]
[JsonSerializable(typeof(ControllerConnection))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(bool))]
public sealed partial class SignalrPtzLancCameraStateContext : JsonSerializerContext;
