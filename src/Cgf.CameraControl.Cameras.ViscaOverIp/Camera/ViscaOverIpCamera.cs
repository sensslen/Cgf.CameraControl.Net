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
    private readonly TimeProvider _time;
    private readonly ITimer _expiry;
    private readonly List<ViscaCategory> _order = [];
    private readonly Dictionary<ViscaCategory, byte[]> _queued = [];
    private readonly Dictionary<int, Command> _sockets = [];
    private readonly Dictionary<ViscaCategory, byte[]> _accepted = [];
    private readonly IDisposable _replies;
    private readonly IDisposable _connection;

    private bool _connected;
    private Command? _inFlight;
    private long _sentAt;
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
        _time = time ?? TimeProvider.System;
        _expiry = _time.CreateTimer(_ => Expire(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _replies = transport.Received.Subscribe(OnReceived);
        _connection = transport.WhenConnectedChanged.Subscribe(OnConnectionChanged);
        transport.Start();
    }

    public string ConnectionString => $"visca://{_config.Ip}:{_config.Port}";

    public IObservable<bool> WhenConnectedChanged => _transport.WhenConnectedChanged;

    public void Pan(double value)
    {
        _pan = Scale(value, PanTiltMaximum(ViscaPacket.MaximumPanSpeed));
        EnqueuePanTilt();
    }

    public void Tilt(double value)
    {
        _tilt = Scale(value, PanTiltMaximum(ViscaPacket.MaximumTiltSpeed));
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
            // refuses, once per selection change, for the length of a service. A command in flight
            // is newer than the last one the camera took, so it is the level to compare against.
            var last = _inFlight is { } sending && sending.Category == category
                ? sending.Packet
                : _accepted.GetValueOrDefault(category);
            if (last is not null && last.AsSpan().SequenceEqual(packet))
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
            _inFlight = new Command(category, packet);
            _sentAt = _time.GetTimestamp();
        }

        _expiry.Change(CommandTimeout, Timeout.InfiniteTimeSpan);
        _transport.Send(packet);
    }

    /// A command the camera took, or refused as one it cannot run, counts as sent. One that was lost
    /// or bounced off full buffers goes out again: the stick that asked for it is usually at rest by
    /// now, and nothing else will repeat a stop.
    private Command? Release(bool resend, bool expired = false)
    {
        Command? released;
        lock (_gate)
        {
            // An expiry that fired just as an answer released its command must not take the next one.
            if (expired && _time.GetElapsedTime(_sentAt) < CommandTimeout)
            {
                return null;
            }

            _expiry.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            released = _inFlight;
            _inFlight = null;
            if (released is { } command)
            {
                if (resend)
                {
                    Requeue(command);
                }
                else
                {
                    _accepted[command.Category] = command.Packet;
                }
            }
        }

        Pump();
        return released;
    }

    /// Called with _gate held. A newer packet for the same axis already says where it should be.
    private void Requeue(Command command)
    {
        if (_queued.ContainsKey(command.Category) || _inFlight?.Category == command.Category)
        {
            return;
        }

        _order.Add(command.Category);
        _queued[command.Category] = command.Packet;
    }

    private void Expire()
    {
        if (Release(resend: true, expired: true) is { } lost)
        {
            _logger.Error(
                "ViscaOverIpCamera",
                $"({_config.Ip}) no answer to {Name(lost.Category)} within {CommandTimeout.TotalSeconds:0} seconds");
        }
    }

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
                    if (Release(resend: false) is { } accepted)
                    {
                        lock (_gate)
                        {
                            _sockets[socket] = accepted;
                        }
                    }

                    break;
                case ViscaReply.Completed:
                    // The acknowledgement already opened the gate for this buffer. Opening it again
                    // would release whichever command went out after it, unanswered.
                    bool acknowledged;
                    lock (_gate)
                    {
                        acknowledged = _sockets.Remove(socket);
                    }

                    if (!acknowledged)
                    {
                        Release(resend: false);
                    }

                    break;
                case ViscaReply.Failed:
                    // The reply itself goes into the message. Which buffer a camera answers on is
                    // not something every model agrees about, so the bytes are what settle an
                    // argument about who refused what.
                    _logger.Error(
                        "ViscaOverIpCamera",
                        $"({_config.Ip}) {Name(Refuse(socket, ViscaPacket.IsTransient(packet)))} was refused - " +
                        $"{ViscaPacket.Describe(packet)} (reply {Convert.ToHexString(packet)})");
                    break;
                default:
                    break;
            }
        }
    }

    /// A refusal that follows an acknowledgement is reported against the buffer that holds the
    /// command, not against whatever is in flight now, so the buffer is what names it. Any other
    /// refusal is for the command in flight.
    private ViscaCategory? Refuse(int socket, bool resend)
    {
        Command buffered;
        lock (_gate)
        {
            if (!_sockets.Remove(socket, out buffered))
            {
                buffered = default;
            }
            else if (resend
                && _accepted.TryGetValue(buffered.Category, out var newest)
                && ReferenceEquals(newest, buffered.Packet))
            {
                _accepted.Remove(buffered.Category);
                Requeue(buffered);
            }
        }

        if (buffered.Packet is null)
        {
            return Release(resend)?.Category;
        }

        Pump();
        return buffered.Category;
    }

    private void OnConnectionChanged(bool connected)
    {
        lock (_gate)
        {
            _connected = connected;
            if (!connected)
            {
                // Whatever was in flight will never be answered, and a camera that went away has
                // emptied its buffers. What is still queued stays, and the lost command joins it:
                // each entry holds the latest position of that axis rather than a step along the
                // way, so sending it once the camera is back is what puts the camera where the
                // stick is.
                if (_inFlight is { } lost)
                {
                    _inFlight = null;
                    Requeue(lost);
                }

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

    // VISCA cannot move below speed 1, even when the percentage asks for less.
    private double PanTiltMaximum(int maximum) => Math.Max(1, maximum * _config.PanTiltSpeedPercent / 100.0);

    private static int Scale(double value, double maximum) =>
        (int)Math.Round(Math.Clamp(value, -1, 1) * maximum);

    private readonly record struct Command(ViscaCategory Category, byte[] Packet);
}
