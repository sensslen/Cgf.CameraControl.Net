using Cgf.CameraControl.Core.GenericFactory;

namespace Cgf.CameraControl.Core.VideoMixer;

public interface IVideoMixer : IConnectionProvider, IImageSelectionChange, IAsyncDisposable
{
    string ConnectionString { get; }

    void Cut();

    void Auto();

    void ChangeInput(int newInput);

    void ToggleKey(int key);

    void RunMacro(int macro);

    Task<bool> IsKeySetAsync(int key, CancellationToken cancellationToken);

    Task<int> GetAuxiliarySelectionAsync(int aux, CancellationToken cancellationToken);
}
