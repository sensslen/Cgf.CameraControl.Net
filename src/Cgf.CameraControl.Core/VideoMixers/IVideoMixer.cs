using Cgf.CameraControl.Core.Connections;

namespace Cgf.CameraControl.Core.VideoMixers;

public readonly record struct PreviewChange(int Input, bool OnAir);

public interface IVideoMixer : IConnectionProvider, IAsyncDisposable
{
    string ConnectionString { get; }

    IObservable<PreviewChange> WhenPreviewChanged { get; }

    IObservable<int> WhenProgramChanged { get; }

    void Cut();

    void Auto();

    void ChangeInput(int input);

    void ToggleKey(int key);

    void RunMacro(int macro);

    Task<bool> IsKeySetAsync(int key, CancellationToken cancellationToken);

    Task<int> GetAuxiliarySelectionAsync(int aux, CancellationToken cancellationToken);
}
