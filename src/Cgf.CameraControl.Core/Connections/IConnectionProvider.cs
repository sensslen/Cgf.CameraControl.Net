namespace Cgf.CameraControl.Core.Connections;

public interface IConnectionProvider
{
    IObservable<bool> WhenConnectedChanged { get; }
}
