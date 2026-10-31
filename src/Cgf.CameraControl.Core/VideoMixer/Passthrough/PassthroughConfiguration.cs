using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cgf.CameraControl.Core.VideoMixer.Passthrough;

public sealed record PassthroughConfiguration
{
    public MixerTallyMode TallyMode { get; set; } = MixerTallyMode.PreviewProgram;
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(PassthroughConfiguration))]
public sealed partial class PassthroughConfigurationContext : JsonSerializerContext;
