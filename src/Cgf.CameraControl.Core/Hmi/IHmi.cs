using Cgf.CameraControl.Core.Connections;

namespace Cgf.CameraControl.Core.Hmi;

public interface IHmi : IConnectionProvider, IAsyncDisposable
{
    string Description { get; }
}
