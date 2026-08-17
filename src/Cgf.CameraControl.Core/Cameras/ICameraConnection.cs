using Cgf.CameraControl.Core.Connections;

namespace Cgf.CameraControl.Core.Cameras;

public enum TallyState
{
    Off,
    Preview,
    Program,
}

/// Movement values run from -1 to 1, where the sign is the direction and the magnitude is the speed:
/// pan right, tilt up, zoom in and focus far are positive.
public interface ICameraConnection : IConnectionProvider, IAsyncDisposable
{
    string ConnectionString { get; }

    void Pan(double value);

    void Tilt(double value);

    void Zoom(double value);

    void Focus(double value);

    void SetTally(TallyState state);
}
