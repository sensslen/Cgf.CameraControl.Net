namespace Cgf.CameraControl.Core.CameraConnection;

/// Pan and tilt travel in one absolute command, so they are stored and recalled together.
public readonly record struct PanTiltPosition(int Pan, int Tilt);

/// Where a camera points, in the camera's own units. A part that is null is one the camera did not
/// report or one a scene does not store.
public sealed record CameraPosition(PanTiltPosition? PanTilt, int? Zoom, int? Focus)
{
    public static CameraPosition None { get; } = new(null, null, null);

    public bool IsEmpty => PanTilt is null && Zoom is null && Focus is null;
}

/// A camera that can report where it points and drive back there.
public interface ICameraPositionControl
{
    /// Asks the camera where it is. A part the camera does not answer for comes back null, which is
    /// how a caller finds out what this particular camera supports.
    Task<CameraPosition> ReadPositionAsync(CancellationToken cancellationToken);

    /// Drives to every part of the position that is set and leaves the others where they are.
    void MoveTo(CameraPosition target);
}
