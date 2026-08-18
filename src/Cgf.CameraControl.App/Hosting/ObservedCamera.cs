using System.Reactive.Subjects;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;

namespace Cgf.CameraControl.App.Hosting;

public readonly record struct CameraMovement(double Pan, double Tilt, double Zoom, double Focus);

/// Records what the application last told a camera to do. Nothing in the domain needs to read that
/// back, so instead of widening ICameraConnection the display sits alongside it: the panel then
/// separates "the stick is not reaching the camera" from "the camera is not acting on it", which is
/// the first question asked when a desk misbehaves.
public sealed class ObservedCamera(ICameraConnection inner) : ICameraConnection
{
    private readonly BehaviorSubject<CameraMovement> _movement = new(default);
    private readonly BehaviorSubject<TallyState> _tally = new(TallyState.Off);

    public string ConnectionString => inner.ConnectionString;

    public IObservable<bool> WhenConnectedChanged => inner.WhenConnectedChanged;

    public IObservable<CameraMovement> WhenMovementChanged => _movement;

    public IObservable<TallyState> WhenTallyChanged => _tally;

    public void Pan(double value)
    {
        _movement.OnNext(_movement.Value with { Pan = value });
        inner.Pan(value);
    }

    public void Tilt(double value)
    {
        _movement.OnNext(_movement.Value with { Tilt = value });
        inner.Tilt(value);
    }

    public void Zoom(double value)
    {
        _movement.OnNext(_movement.Value with { Zoom = value });
        inner.Zoom(value);
    }

    public void Focus(double value)
    {
        _movement.OnNext(_movement.Value with { Focus = value });
        inner.Focus(value);
    }

    public void SetTally(TallyState value)
    {
        _tally.OnNext(value);
        inner.SetTally(value);
    }

    public async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync().ConfigureAwait(false);
        _movement.Dispose();
        _tally.Dispose();
    }
}

public sealed class ObservingCameraBuilder(IBuilder<ICameraConnection> inner, ICollection<ObservedCamera> built)
    : IBuilder<ICameraConnection>
{
    public IReadOnlyCollection<string> SupportedTypes => inner.SupportedTypes;

    public async Task<ICameraConnection> BuildAsync(ConfigEntry entry, CancellationToken cancellationToken)
    {
        var camera = new ObservedCamera(await inner.BuildAsync(entry, cancellationToken).ConfigureAwait(false));
        built.Add(camera);
        return camera;
    }
}
