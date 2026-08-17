using Cgf.CameraControl.Core.GenericFactory;

namespace Cgf.CameraControl.Core.CameraConnection;

public enum TallyState
{
    Off,
    Preview,
    Program,
}

public interface ICameraConnection : IConnectionProvider, IAsyncDisposable
{
    string ConnectionString { get; }

    /// Pan speed in [-1 .. 1], where -1 is maximum speed left and 1 is maximum speed right.
    void Pan(double value);

    /// Tilt speed in [-1 .. 1], where -1 is maximum speed down and 1 is maximum speed up.
    void Tilt(double value);

    /// Zoom speed in [-1 .. 1], where -1 is maximum speed out and 1 is maximum speed in.
    void Zoom(double value);

    /// Focus speed in [-1 .. 1], where -1 is maximum speed near and 1 is maximum speed far.
    void Focus(double value);

    void SetTally(TallyState value);
}
