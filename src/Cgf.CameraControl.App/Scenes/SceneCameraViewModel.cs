using System.Globalization;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.App.ViewModels;
using Cgf.CameraControl.Core.CameraConnection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cgf.CameraControl.App.Scenes;

public enum PositionProbe
{
    Checking,
    Supported,
    Unsupported,
}

/// One camera in the scene wizard: what the live probe found it supports, how it was framed here,
/// and which of its parts the scene stores.
public sealed partial class SceneCameraViewModel : ViewModelBase
{
    private readonly ICameraConnection _camera;

    public SceneCameraViewModel(ILocalizer strings, int instance, ICameraConnection camera)
    {
        Strings = strings;
        Instance = instance;
        _camera = camera;
        Probe = camera.PositionControl is null ? PositionProbe.Unsupported : PositionProbe.Checking;
    }

    public ILocalizer Strings { get; }

    public int Instance { get; }

    public string Title => _camera.ConnectionString;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status), nameof(CanFrame), nameof(IsChecking))]
    public partial PositionProbe Probe { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status), nameof(CanStorePanTilt))]
    public partial bool SupportsPanTilt { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status), nameof(CanStoreZoom))]
    public partial bool SupportsZoom { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status), nameof(CanStoreFocus))]
    public partial bool SupportsFocus { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStorePanTilt), nameof(CanStoreZoom), nameof(CanStoreFocus), nameof(WillStore))]
    public partial bool Include { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WillStore))]
    public partial bool StorePanTilt { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WillStore))]
    public partial bool StoreZoom { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WillStore))]
    public partial bool StoreFocus { get; set; }

    /// The framing the edited or base scene holds for this camera, if any.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStored), nameof(CanStorePanTilt), nameof(CanStoreZoom), nameof(CanStoreFocus))]
    public partial CameraPosition? Stored { get; private set; }

    public bool HasStored => Stored is not null;

    public bool IsChecking => Probe == PositionProbe.Checking;

    /// Only a camera that answered the probe is offered for framing, because only its framing can
    /// be stored.
    public bool CanFrame => Probe == PositionProbe.Supported;

    /// A stored part the camera does not answer for can still be kept, because keeping it needs no
    /// answer.
    public bool CanStorePanTilt => Include && (SupportsPanTilt || Stored?.PanTilt is not null);

    public bool CanStoreZoom => Include && (SupportsZoom || Stored?.Zoom is not null);

    public bool CanStoreFocus => Include && (SupportsFocus || Stored?.Focus is not null);

    public bool WillStore => Include && (StorePanTilt || StoreZoom || StoreFocus);

    public bool MovedPanTilt { get; private set; }

    public bool MovedZoom { get; private set; }

    public bool MovedFocus { get; private set; }

    /// Movement since the include step last looked, so returning with Back keeps a part the
    /// operator unticked there unticked unless it is moved again.
    private bool _newPanTilt;
    private bool _newZoom;
    private bool _newFocus;

    public string Status => Probe switch
    {
        PositionProbe.Checking => Strings.Text("scene.checking"),
        PositionProbe.Unsupported when _camera.PositionControl is null => Strings.Text("scene.noPosition"),
        PositionProbe.Unsupported => Strings.Text("scene.noAnswer"),
        _ => string.Join(", ", Parts()),
    };

    /// Asks the camera where it is. What it answers is what it supports, and a part it does not
    /// answer for can only be kept as the scene stores it.
    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        if (_camera.PositionControl is not { } control)
        {
            return;
        }

        var position = await control.ReadPositionAsync(cancellationToken).ConfigureAwait(true);
        SupportsPanTilt = position.PanTilt is not null;
        SupportsZoom = position.Zoom is not null;
        SupportsFocus = position.Focus is not null;
        StorePanTilt &= SupportsPanTilt || Stored?.PanTilt is not null;
        StoreZoom &= SupportsZoom || Stored?.Zoom is not null;
        StoreFocus &= SupportsFocus || Stored?.Focus is not null;
        Include &= !position.IsEmpty;
        Probe = position.IsEmpty ? PositionProbe.Unsupported : PositionProbe.Supported;
    }

    /// Starts the camera from a scene: included with the parts the scene stores for it, or left out.
    public void UseStored(CameraPosition? stored)
    {
        Stored = stored;
        Include = stored is not null && Probe != PositionProbe.Unsupported;
        StorePanTilt = stored?.PanTilt is not null;
        StoreZoom = stored?.Zoom is not null;
        StoreFocus = stored?.Focus is not null;
        MovedPanTilt = MovedZoom = MovedFocus = false;
        _newPanTilt = _newZoom = _newFocus = false;
    }

    public void Move(double pan, double tilt)
    {
        _camera.Pan(pan);
        _camera.Tilt(tilt);
        var moved = pan != 0 || tilt != 0;
        MovedPanTilt |= moved;
        _newPanTilt |= moved;
    }

    public void Lens(double zoom, double focus)
    {
        _camera.Zoom(zoom);
        _camera.Focus(focus);
        MovedZoom |= zoom != 0;
        MovedFocus |= focus != 0;
        _newZoom |= zoom != 0;
        _newFocus |= focus != 0;
    }

    /// Back to what the scene holds, which is then kept rather than captured again.
    public void GoToStored()
    {
        if (Stored is { } stored && _camera.PositionControl is { } control)
        {
            control.MoveTo(stored);
            MovedPanTilt &= stored.PanTilt is null;
            MovedZoom &= stored.Zoom is null;
            MovedFocus &= stored.Focus is null;
            _newPanTilt &= stored.PanTilt is null;
            _newZoom &= stored.Zoom is null;
            _newFocus &= stored.Focus is null;
        }
    }

    /// A part moved in the frame step is one the operator meant to store.
    public void IncludeMoved()
    {
        var panTilt = _newPanTilt;
        var zoom = _newZoom;
        var focus = _newFocus;
        _newPanTilt = _newZoom = _newFocus = false;
        if (!CanFrame || !(panTilt || zoom || focus))
        {
            return;
        }

        Include = true;
        StorePanTilt |= panTilt && SupportsPanTilt;
        StoreZoom |= zoom && SupportsZoom;
        StoreFocus |= focus && SupportsFocus;
    }

    public void Stop()
    {
        _camera.Pan(0);
        _camera.Tilt(0);
        _camera.Zoom(0);
        _camera.Focus(0);
    }

    /// What the scene stores for this camera: a moved part, or one with nothing stored yet, is read
    /// from the camera now; any other ticked part keeps its stored value. Null when nothing is
    /// stored. Throws with the part the camera did not report.
    public async Task<CameraPosition?> ComposeAsync(CancellationToken cancellationToken)
    {
        if (!WillStore || _camera.PositionControl is not { } control)
        {
            return null;
        }

        var readPanTilt = StorePanTilt && (MovedPanTilt || Stored?.PanTilt is null);
        var readZoom = StoreZoom && (MovedZoom || Stored?.Zoom is null);
        var readFocus = StoreFocus && (MovedFocus || Stored?.Focus is null);
        var read = readPanTilt || readZoom || readFocus
            ? await control.ReadPositionAsync(cancellationToken).ConfigureAwait(true)
            : CameraPosition.None;

        var panTilt = StorePanTilt ? (readPanTilt ? read.PanTilt : Stored?.PanTilt) : null;
        var zoom = StoreZoom ? (readZoom ? read.Zoom : Stored?.Zoom) : null;
        var focus = StoreFocus ? (readFocus ? read.Focus : Stored?.Focus) : null;
        Missing(StorePanTilt && panTilt is null, "surface.panTilt");
        Missing(StoreZoom && zoom is null, "surface.zoom");
        Missing(StoreFocus && focus is null, "surface.focus");
        return new CameraPosition(panTilt, zoom, focus);
    }

    private void Missing(bool missing, string part)
    {
        if (missing)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.CurrentUICulture,
                Strings.Text("scene.captureFailed"),
                Title,
                Strings.Text(part)));
        }
    }

    private IEnumerable<string> Parts()
    {
        if (SupportsPanTilt)
        {
            yield return Strings.Text("surface.panTilt");
        }

        if (SupportsZoom)
        {
            yield return Strings.Text("surface.zoom");
        }

        if (SupportsFocus)
        {
            yield return Strings.Text("surface.focus");
        }
    }
}
