namespace Cgf.CameraControl.Core.GenericFactory;

public interface IConnectionProvider
{
    IObservable<bool> WhenConnectedChanged { get; }
}
