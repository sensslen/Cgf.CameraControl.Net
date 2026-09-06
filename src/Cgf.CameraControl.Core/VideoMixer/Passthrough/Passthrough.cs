using System.Reactive.Linq;
using System.Reactive.Subjects;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Core.VideoMixer.Passthrough;

/// A switcher that only remembers what it was told. It is how the control surface is exercised
/// without a desk on the network: everything upstream of the mixer behaves exactly as it does on an
/// ATEM, so a pad, its bindings and the cameras can all be checked on a laptop.
public sealed class Passthrough(ILogger logger) : IVideoMixer
{
    private readonly BehaviorSubject<PreviewChange> _preview = new(new PreviewChange(-1, false));
    private readonly BehaviorSubject<int> _program = new(-1);

    public string ConnectionString => "passthrough";

    public IObservable<bool> WhenConnectedChanged => Observable.Return(true);

    public IObservable<PreviewChange> WhenPreviewChanged => _preview;

    public IObservable<int> WhenProgramChanged => _program;

    public void Cut() => Transition("cut");

    public void Auto() => Transition("auto");

    public void ChangeInput(int newInput)
    {
        Log($"changing input to: {newInput}");
        _preview.OnNext(new PreviewChange(newInput, false));
    }

    public void ToggleKey(int key) => Log($"toggling key: {key}");

    public void RunMacro(int macro) => Log($"running macro: {macro}");

    public Task<bool> IsKeySetAsync(int key, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<int> GetAuxiliarySelectionAsync(int aux, CancellationToken cancellationToken) => Task.FromResult(0);

    public ValueTask DisposeAsync()
    {
        _preview.Dispose();
        _program.Dispose();
        return ValueTask.CompletedTask;
    }

    private void Transition(string kind)
    {
        var wasOnAir = _program.Value;
        Log($"performing {kind} transition from {wasOnAir} to {_preview.Value.Preview}");
        _program.OnNext(_preview.Value.Preview);
        _preview.OnNext(new PreviewChange(wasOnAir, false));
    }

    private void Log(string message) => logger.Log("Passthrough video mixer", message);
}
