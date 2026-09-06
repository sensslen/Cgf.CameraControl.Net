using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Cameras.ViscaOverIp.Camera;

public sealed class ViscaOverIpCamera : ICameraConnection
{
    /// A camera that never answers must not wedge the gate shut for the rest of the service.
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(5);

    private readonly ViscaOverIpCameraConfiguration _config;
    private readonly IViscaTransport _transport;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();
    private readonly ITimer _expiry;
    private readonly List<ViscaCategory> _order = [];
    private readonly Dictionary<ViscaCategory, byte[]> _queued = [];
    private readonly Dictionary<int, ViscaCategory> _sockets = [];
    private readonly Dictionary<ViscaCategory, byte[]> _accepted = [];
    private readonly IDisposable _replies;
    private readonly IDisposable _connection;

    private bool _connected;
    private ViscaCategory? _inFlight;
    private ViscaCategory? _lastSent;
    private int _pan;
    private int _tilt;

    public ViscaOverIpCamera(
        ViscaOverIpCameraConfiguration config,
        IViscaTransport transport,
        ILogger logger,
        TimeProvider? time = null)
    {
        _config = config;
        _transport = transport;
        _logger = logger;
        _expiry = (time ?? TimeProvider.System)
            .CreateTimer(_ => Expire(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _replies = transport.Received.Subscribe(OnReceived);
        _connection = transport.WhenConnectedChanged.Subscribe(OnConnectionChanged);
        transport.Start();
    }

    public string ConnectionString => $"visca://{_config.Ip}:{_config.Port}";

    public IObservable<bool> WhenConnectedChanged => _transport.WhenConnectedChanged;

    public void Pan(double value)
    {
        _pan = Scale(value, ViscaPacket.MaximumPanSpeed);
        EnqueuePanTilt();
    }

    public void Tilt(double value)
    {
        _tilt = Scale(value, ViscaPacket.MaximumTiltSpeed);
        EnqueuePanTilt();
    }

    public void Zoom(double value) =>
        Enqueue(ViscaCategory.Zoom, ViscaPacket.Zoom(Scale(value, ViscaPacket.LensSpeeds)));

    public void Focus(double value) =>
        Enqueue(ViscaCategory.Focus, ViscaPacket.Focus(Scale(value, ViscaPacket.LensSpeeds)));

    public void SetTally(TallyState value)
    {
        var payload = ViscaTally.Payload(_config.TallyMode, value);
        if (payload is null)
        {
            return;
        }

        Enqueue(ViscaCategory.Tally, payload);
    }

    public async ValueTask DisposeAsync()
    {
        _replies.Dispose();
        _connection.Dispose();
        _expiry.Dispose();
        await _transport.DisposeAsync().ConfigureAwait(false);
    }

    /// Both axes travel in one command, so a stick held on the diagonal moves the camera on the
    /// diagonal rather than alternating between two single axis commands.
    private void EnqueuePanTilt() => Enqueue(ViscaCategory.PanTilt, ViscaPacket.PanTilt(Invert(_pan), Invert(_tilt)));

    private void Enqueue(ViscaCategory category, byte[] packet)
    {
        lock (_gate)
        {
            // A drive command is a level, not a step: the camera holds it until told otherwise, so
            // sending the same one again asks for nothing. Interfaces stop every axis of a camera
            // they let go of, which on a camera in auto focus means a focus stop it cannot run and
            // refuses, once per selection change, for the length of a service.
            if (_accepted.TryGetValue(category, out var last) && last.AsSpan().SequenceEqual(packet))
            {
                if (_queued.Remove(category))
                {
                    _order.Remove(category);
                }

                return;
            }

            if (!_queued.ContainsKey(category))
            {
                _order.Add(category);
            }

            _queued[category] = packet;
        }

        Pump();
    }

    /// One command is in flight at a time: VISCA cameras hold two command buffers and answer a third
    /// with a buffer-full error, so the next packet waits for this one to be acknowledged.
    private void Pump()
    {
        byte[] packet;
        lock (_gate)
        {
            if (_inFlight is not null || !_connected || _order.Count == 0)
            {
                return;
            }

            var category = _order[0];
            _order.RemoveAt(0);
            packet = _queued[category];
            _queued.Remove(category);
            _inFlight = category;
            _lastSent = category;
            _accepted[category] = packet;
        }

        _expiry.Change(CommandTimeout, Timeout.InfiniteTimeSpan);
        _transport.Send(packet);
    }

    private ViscaCategory? Release()
    {
        _expiry.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        ViscaCategory? released;
        lock (_gate)
        {
            released = _inFlight;
            _inFlight = null;
        }

        Pump();
        return released;
    }

    private void Expire() =>
        _logger.Error($"ViscaOverIpCamera({_config.Ip}):no answer to {Name(Release())} within {CommandTimeout.TotalSeconds:0} seconds");

    /// A refusal only says what went wrong, never which command it went wrong for, and the answer
    /// arrives after the request is gone. Naming the command here is the difference between a log
    /// that says the camera is unhappy and one that says which axis it will not move.
    private static string Name(ViscaCategory? category) => category switch
    {
        ViscaCategory.PanTilt => "the pan and tilt command",
        ViscaCategory.Zoom => "the zoom command",
        ViscaCategory.Focus => "the focus command",
        ViscaCategory.Tally => "the tally command",
        _ => "a command",
    };

    private void OnReceived(byte[] datagram)
    {
        foreach (var packet in ViscaPacket.Split(datagram))
        {
            var socket = packet.Length < 2 ? 0 : packet[1] & 0x0F;
            switch (ViscaPacket.Classify(packet))
            {
                case ViscaReply.Acknowledged:
                    // The camera has taken the command into one of its two buffers and will report
                    // how it went against that buffer, which can be several commands later. The gate
                    // opens here, and which command the buffer holds is remembered for that report.
                    if (Release() is { } accepted)
                    {
                        lock (_gate)
                        {
                            _sockets[socket] = accepted;
                        }
                    }

                    break;
                case ViscaReply.Completed:
                    Forget(socket);
                    Release();
                    break;
                case ViscaReply.Failed:
                    // The reply itself goes into the message. Which buffer a camera answers on is
                    // not something every model agrees about, so the bytes are what settle an
                    // argument about who refused what.
                    _logger.Error(
                        $"ViscaOverIpCamera({_config.Ip}):{NameIn(socket)} was refused - " +
                        $"{ViscaPacket.Describe(packet)} (reply {Convert.ToHexString(packet)})");
                    break;
                default:
                    break;
            }
        }
    }

    /// A refusal that follows an acknowledgement is reported against the buffer that holds the
    /// command, not against whatever is in flight now, so the buffer is what names it. A camera that
    /// answers on a buffer it never acknowledged leaves only the command last sent to point at.
    private string NameIn(int socket)
    {
        lock (_gate)
        {
            if (_sockets.Remove(socket, out var category))
            {
                return Name(category);
            }
        }

        return Name(Release() ?? _lastSent);
    }

    private void Forget(int socket)
    {
        lock (_gate)
        {
            _sockets.Remove(socket);
        }
    }

    private void OnConnectionChanged(bool connected)
    {
        lock (_gate)
        {
            _connected = connected;
            if (!connected)
            {
                // Whatever was in flight will never be answered, and a camera that went away has
                // emptied its buffers. What is still queued stays: each entry holds the latest
                // position of that axis rather than a step along the way, so sending it once the
                // camera is back is what puts the camera where the stick is.
                _inFlight = null;
                _lastSent = null;
                _sockets.Clear();

                // A camera that went away may come back having forgotten everything, so nothing it
                // was told before still counts as sent.
                _accepted.Clear();
            }
        }

        if (connected)
        {
            Pump();
        }
        else
        {
            _expiry.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    private int Invert(int value) => _config.PanTiltInvert ? -value : value;

    private static int Scale(double value, int maximum) =>
        (int)Math.Round(Math.Clamp(value, -1, 1) * maximum);
}
