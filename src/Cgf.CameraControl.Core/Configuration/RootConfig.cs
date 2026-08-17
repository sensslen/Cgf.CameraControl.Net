namespace Cgf.CameraControl.Core.Configuration;

public sealed record RootConfig(
    IReadOnlyList<ConfigEntry> Cams,
    IReadOnlyList<ConfigEntry> VideoMixers,
    IReadOnlyList<ConfigEntry> Interfaces)
{
    public static RootConfig Empty { get; } = new([], [], []);
}
