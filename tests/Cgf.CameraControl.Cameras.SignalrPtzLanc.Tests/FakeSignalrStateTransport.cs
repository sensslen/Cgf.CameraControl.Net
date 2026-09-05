using System.Reactive.Subjects;
using Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;

namespace Cgf.CameraControl.Cameras.SignalrPtzLanc.Tests;

/// The hub call is left hanging until the test answers it, which is what makes the camera's
/// one-call-at-a-time gate observable without waiting on anything.
public sealed class FakeSignalrStateTransport : ISignalrStateTransport
{
    private readonly BehaviorSubject<bool> _connected = new(false);

    private TaskCompletionSource<bool>? _inFlight;

    public bool Started { get; private set; }

    public List<SignalrPtzLancCameraState> Sent { get; } = [];

    public IObservable<bool> WhenConnectedChanged => _connected;

    public void Start() => Started = true;

    public Task<bool> SetStateAsync(SignalrPtzLancCameraState state, CancellationToken cancellationToken)
    {
        Sent.Add(state);
        _inFlight = new TaskCompletionSource<bool>();
        return _inFlight.Task;
    }

    public void Connect() => _connected.OnNext(true);

    public void Disconnect() => _connected.OnNext(false);

    /// The controller applied the state.
    public void Accept() => Answer(tcs => tcs.SetResult(true));

    /// The controller could not apply it and expects it again.
    public void Reject() => Answer(tcs => tcs.SetResult(false));

    public void Fail(string message) => Answer(tcs => tcs.SetException(new InvalidOperationException(message)));

    public ValueTask DisposeAsync()
    {
        _connected.Dispose();
        return ValueTask.CompletedTask;
    }

    private void Answer(Action<TaskCompletionSource<bool>> answer)
    {
        var pending = _inFlight ?? throw new InvalidOperationException("no hub call is waiting for an answer");
        _inFlight = null;
        answer(pending);
    }
}
