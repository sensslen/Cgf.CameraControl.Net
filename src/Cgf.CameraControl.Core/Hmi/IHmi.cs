using Cgf.CameraControl.Core.GenericFactory;

namespace Cgf.CameraControl.Core.Hmi;

public interface IHmi : IConnectionProvider, IAsyncDisposable
{
    string Description { get; }
}
