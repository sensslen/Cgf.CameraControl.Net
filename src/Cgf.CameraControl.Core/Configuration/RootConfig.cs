namespace Cgf.CameraControl.Core.Configuration;

/// The whole file: one list per section, each entry typed by the record its `type` names, or kept as
/// an invalid entry when it could not be read.
public sealed record RootConfig(
    IReadOnlyList<CameraConfiguration> Cams,
    IReadOnlyList<VideoMixerConfiguration> VideoMixers,
    IReadOnlyList<InterfaceConfiguration> Interfaces)
{
    public static RootConfig Empty { get; } = new([], [], []);
}
