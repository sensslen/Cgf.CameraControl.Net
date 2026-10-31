using System.Text.Json.Serialization;

namespace Cgf.CameraControl.Core.VideoMixer;

[JsonConverter(typeof(MixerTallyModeConverter))]
public enum MixerTallyMode
{
    [JsonStringEnumMemberName("preview-program")]
    PreviewProgram,

    [JsonStringEnumMemberName("program")]
    Program,

    [JsonStringEnumMemberName("none")]
    None,
}

public sealed class MixerTallyModeConverter() : JsonStringEnumConverter<MixerTallyMode>(allowIntegerValues: false);
