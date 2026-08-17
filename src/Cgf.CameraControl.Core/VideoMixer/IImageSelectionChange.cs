namespace Cgf.CameraControl.Core.VideoMixer;

/// The TypeScript side models these as a StrictEventEmitter with previewChange and programChange
/// events. They are observables here so the UI can subscribe to the same stream the HMI consumes
/// instead of tracking a second copy of the state.
public interface IImageSelectionChange
{
    IObservable<PreviewChange> WhenPreviewChanged { get; }

    IObservable<int> WhenProgramChanged { get; }
}

public readonly record struct PreviewChange(int Preview, bool OnAir);
