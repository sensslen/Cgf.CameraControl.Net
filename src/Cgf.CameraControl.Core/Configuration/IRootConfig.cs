using System.Text.Json;

namespace Cgf.CameraControl.Core.Configuration;

public sealed record RootConfig(
    IReadOnlyList<ConfigEntry> Cams,
    IReadOnlyList<ConfigEntry> VideoMixers,
    IReadOnlyList<ConfigEntry> Interfaces)
{
    public static RootConfig Empty { get; } = new([], [], []);

    /// The whole file as it was read, so a save keeps what the loader could not take.
    public JsonElement? Source { get; init; }
}
