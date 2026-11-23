using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.CameraConnection;

namespace Cgf.CameraControl.Core.Scenes;

/// A named set of framings, one per camera instance, each storing only the parts chosen for it.
public sealed record CameraScene(
    [property: JsonRequired] string Name,
    [property: JsonRequired] IReadOnlyDictionary<int, ScenePosition> Cameras)
{
    /// Two names an operator would read as the same scene are the same scene.
    public static bool SameName(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// A stored framing as the file writes it: pan and tilt as two keys, although a camera only ever
/// reports and drives them together.
public sealed record ScenePosition(int? Pan, int? Tilt, int? Zoom, int? Focus)
{
    [JsonIgnore]
    public CameraPosition Position =>
        new(Pan is { } pan && Tilt is { } tilt ? new PanTiltPosition(pan, tilt) : null, Zoom, Focus);

    public static ScenePosition From(CameraPosition position) =>
        new(position.PanTilt?.Pan, position.PanTilt?.Tilt, position.Zoom, position.Focus);
}
