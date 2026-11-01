using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Logger;
using System.Reactive.Subjects;

namespace Cgf.CameraControl.Cameras.ViscaOverIp.Camera;

public sealed class ViscaOverIpCamera : ICameraConnection, ICameraColorControl
{
    /// A camera that never answers must not wedge the gate shut for the rest of the service.
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(5);

    private readonly ViscaOverIpCameraConfiguration _config;
    private readonly IViscaTransport _transport;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();
    private readonly TimeProvider _time;
    private readonly ITimer _expiry;
    private readonly ITimer _colorCompletionExpiry;
    private readonly ITimer _inquiryExpiry;
    private readonly List<Command> _queue = [];
    private readonly Dictionary<int, Command> _sockets = [];
    private readonly HashSet<int> _expiredColorSockets = [];
    private readonly Dictionary<ViscaCategory, byte[]> _accepted = [];
    private readonly BehaviorSubject<CameraColorState?> _color = new(null);
    private readonly IDisposable _replies;
    private readonly IDisposable _connection;

    private bool _connected;
    private Command? _inFlight;
    /// Inquiries are answered with data rather than an acknowledgement, so they wait here instead of
    /// in _inFlight and a slow or lost answer cannot hold back a movement stop.
    private Command? _inquiry;
    private long _sentAt;
    private int _pan;
    private int _tilt;
    private int? _whiteBalance;
    private int? _redGain;
    private int? _blueGain;
    private bool _refreshPending;

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
        _colorCompletionExpiry = _time.CreateTimer(_ => ExpireColorCompletion(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _inquiryExpiry = _time.CreateTimer(_ => ExpireInquiry(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _replies = transport.Received.Subscribe(OnReceived);
        _connection = transport.WhenConnectedChanged.Subscribe(OnConnectionChanged);
        transport.Start();
    }

    public string ConnectionString => $"visca://{_config.Ip}:{_config.Port}";

    public IObservable<bool> WhenConnectedChanged => _transport.WhenConnectedChanged;

    public IObservable<CameraColorState?> WhenColorChanged => _color;

    public void RefreshColor()
    {
        lock (_gate)
        {
            if (!_connected)
            {
                return;
            }

            _refreshPending = true;
            _color.OnNext(null);
            StartPendingReadback();
        }
        Pump();
    }

    private void StartPendingReadback()
    {
        if (!_connected || !_refreshPending
            || _queue.Any(command => IsColorCategory(command.Category))
            || _inFlight is { } pending && IsColorCategory(pending.Category)
            || _inquiry is not null
            || _sockets.Values.Any(command => IsColorCorrection(command.Category)))
        {
            return;
        }

        _refreshPending = false;
        _whiteBalance = null;
        _redGain = null;
        _blueGain = null;
        _queue.Add(new Command(ViscaCategory.WhiteBalanceInquiry, ViscaPacket.WhiteBalanceInquiry(), 0));
        _queue.Add(new Command(ViscaCategory.RedGainInquiry, ViscaPacket.RedGainInquiry(), 0));
        _queue.Add(new Command(ViscaCategory.BlueGainInquiry, ViscaPacket.BlueGainInquiry(), 0));
    }

    public void OnePushWhiteBalance()
    {
        lock (_gate)
        {
            var state = RequireColor();
            EnqueueColorAction(ViscaPacket.OnePushWhiteBalanceMode());
            EnqueueColorAction(ViscaPacket.OnePushWhiteBalanceTrigger());
            _color.OnNext(state with { IsManual = false });
        }
    }

    public void SetManualWhiteBalance(bool manual)
    {
        lock (_gate)
        {
            var state = RequireColor();
            EnqueueColorAction(ViscaPacket.WhiteBalance(manual));
            _color.OnNext(state with { IsManual = manual });
        }
    }

    public void SetRedGain(int value) => SetColorGain(ViscaCategory.RedGain, ViscaPacket.RedGain(value), value);

    public void SetBlueGain(int value) => SetColorGain(ViscaCategory.BlueGain, ViscaPacket.BlueGain(value), value);

    private void SetColorGain(ViscaCategory category, byte[] packet, int value)
    {
        lock (_gate)
        {
            var state = RequireColor();
            if (!state.IsManual)
            {
                throw new InvalidOperationException("Color gains require manual white balance.");
            }

            Enqueue(category, packet);
            _color.OnNext(category == ViscaCategory.RedGain ? state with { RedGain = value } : state with { BlueGain = value });
        }
    }

    private CameraColorState RequireColor() => _connected && _color.Value is { } state
        ? state
        : throw new InvalidOperationException("The camera has not reported support for color correction.");

    private void EnqueueColorAction(byte[] packet)
    {
        _queue.Add(new Command(ViscaCategory.ColorAction, packet, 0));
        Pump();
    }

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
        _colorCompletionExpiry.Dispose();
        _inquiryExpiry.Dispose();
        await _transport.DisposeAsync().ConfigureAwait(false);
        _color.Dispose();
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
            var gain = category is ViscaCategory.RedGain or ViscaCategory.BlueGain;
            var barrier = gain ? _queue.FindLastIndex(command => command.Category == ViscaCategory.ColorAction) : -1;
            var queuedIndex = _queue.FindLastIndex(command => command.Category == category);
            if (gain && (barrier >= 0 || _inFlight?.Category == ViscaCategory.ColorAction))
            {
                last = null;
            }
            if (!ViscaPacket.IsColorInquiry(category) && last is not null && last.AsSpan().SequenceEqual(packet))
            {
                if (queuedIndex > barrier)
                {
                    _queue.RemoveAt(queuedIndex);
                }

                return;
            }

            if (queuedIndex > barrier && !IsMovementStop(category, packet))
            {
                _queue[queuedIndex] = new Command(category, packet, 0);
            }
            else
            {
                if (queuedIndex > barrier)
                {
                    _queue.RemoveAt(queuedIndex);
                }
                if (IsMovementStop(category, packet))
                {
                    var colorIndex = _queue.FindIndex(command => IsColorCategory(command.Category));
                    _queue.Insert(colorIndex < 0 ? _queue.Count : colorIndex, new Command(category, packet, 0));
                }
                else
                {
                    _queue.Add(new Command(category, packet, 0));
                }
            }
        }

        Pump();
    }

    /// VISCA cameras reject commands when both buffers are full. ACK allows movement to advance,
    /// but color commands wait for completion so later modes cannot change earlier corrections.
    /// Sending inside the gate keeps the recorded order of the inquiry and the command in flight the
    /// order they reach the camera, which is what attributes a refusal on socket 0.
    private void Pump()
    {
        lock (_gate)
        {
            while (_inFlight is null && _connected && _queue.Count != 0)
            {
                var index = _inquiry is not null || _sockets.Values.Any(command => IsColorCorrection(command.Category))
                    ? _queue.FindIndex(command => !IsColorCategory(command.Category))
                    : 0;
                if (index < 0)
                {
                    return;
                }
                var command = _queue[index];
                _queue.RemoveAt(index);
                if (command.Category == ViscaCategory.ColorAction)
                {
                    _accepted.Remove(ViscaCategory.RedGain);
                    _accepted.Remove(ViscaCategory.BlueGain);
                }
                if (ViscaPacket.IsColorInquiry(command.Category))
                {
                    _inquiry = command with { SentAt = _time.GetTimestamp() };
                    _inquiryExpiry.Change(CommandTimeout, Timeout.InfiniteTimeSpan);
                }
                else
                {
                    _sentAt = _time.GetTimestamp();
                    _inFlight = command with { SentAt = _sentAt };
                    _expiry.Change(CommandTimeout, Timeout.InfiniteTimeSpan);
                }
                _transport.Send(command.Packet);
            }
        }
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
                    if (expired && IsColorCorrection(command.Category))
                    {
                        DiscardColorCommands();
                        _refreshPending = true;
                        _color.OnNext(null);
                    }
                    else
                    {
                        Requeue(command);
                    }
                }
                else if (command.Category != ViscaCategory.ColorAction)
                {
                    _accepted[command.Category] = command.Packet;
                }
            }
            StartPendingReadback();
        }

        Pump();
        return released;
    }

    /// An inquiry that is refused for lack of room goes out again. One that was lost is not retried:
    /// a camera that does not answer it is one that does not support color correction.
    private Command? ReleaseInquiry(bool resend, bool expired = false)
    {
        Command released;
        lock (_gate)
        {
            if (_inquiry is not { } inquiry || expired && _time.GetElapsedTime(inquiry.SentAt) < CommandTimeout)
            {
                return null;
            }

            _inquiryExpiry.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _inquiry = null;
            released = inquiry;
            if (resend && !expired)
            {
                Requeue(inquiry);
            }
            StartPendingReadback();
        }

        Pump();
        return released;
    }

    /// Called with _gate held. A newer packet for the same axis already says where it should be.
    private void Requeue(Command command)
    {
        if (IsColorCorrection(command.Category))
        {
            if (command.Category is ViscaCategory.RedGain or ViscaCategory.BlueGain
                && _queue.TakeWhile(queued => queued.Category != ViscaCategory.ColorAction)
                    .Any(queued => queued.Category == command.Category))
            {
                return;
            }
            var colorIndex = _queue.FindIndex(queued => IsColorCategory(queued.Category));
            _queue.Insert(colorIndex < 0 ? _queue.Count : colorIndex, command);
            return;
        }

        if (_queue.Any(queued => queued.Category == command.Category) || _inFlight?.Category == command.Category)
        {
            return;
        }

        if (IsMovementStop(command.Category, command.Packet))
        {
            var colorIndex = _queue.FindIndex(queued => IsColorCategory(queued.Category));
            _queue.Insert(colorIndex < 0 ? _queue.Count : colorIndex, command);
        }
        else
        {
            _queue.Add(command);
        }
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

    private void ExpireInquiry()
    {
        if (ReleaseInquiry(resend: true, expired: true) is { } lost)
        {
            _logger.Error(
                "ViscaOverIpCamera",
                $"({_config.Ip}) no answer to {Name(lost.Category)} within {CommandTimeout.TotalSeconds:0} seconds");
        }
    }

    private void ExpireColorCompletion()
    {
        lock (_gate)
        {
            var lost = _sockets.Where(entry => IsColorCorrection(entry.Value.Category)
                && _time.GetElapsedTime(entry.Value.SentAt) >= CommandTimeout).ToArray();
            foreach (var entry in lost)
            {
                _sockets.Remove(entry.Key);
                _expiredColorSockets.Add(entry.Key);
            }

            if (lost.Length != 0)
            {
                _logger.Error("ViscaOverIpCamera", $"({_config.Ip}) no completion for {Name(lost[0].Value.Category)}; reading current settings");
                DiscardColorCommands();
                RefreshColor();
            }

            if (!_sockets.Values.Any(command => IsColorCorrection(command.Category)))
            {
                _colorCompletionExpiry.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
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
        ViscaCategory.ColorAction => "the white balance command",
        ViscaCategory.RedGain => "the red gain command",
        ViscaCategory.BlueGain => "the blue gain command",
        ViscaCategory.WhiteBalanceInquiry => "the white balance inquiry",
        ViscaCategory.RedGainInquiry => "the red gain inquiry",
        ViscaCategory.BlueGainInquiry => "the blue gain inquiry",
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
                    lock (_gate)
                    {
                        if (_inFlight is not { } accepted)
                        {
                            break;
                        }
                        _sockets[socket] = accepted;
                        if (IsColorCorrection(accepted.Category))
                        {
                            _colorCompletionExpiry.Change(CommandTimeout, CommandTimeout);
                        }
                        Release(resend: false);
                    }

                    break;
                case ViscaReply.Completed:
                    // The acknowledgement already opened the gate for this buffer. Opening it again
                    // would release whichever command went out after it, unanswered.
                    bool acknowledged;
                    Command? completed = null;
                    lock (_gate)
                    {
                        acknowledged = _sockets.Remove(socket, out var buffered);
                        if (acknowledged)
                        {
                            completed = buffered;
                        }
                        acknowledged |= _expiredColorSockets.Contains(socket);
                    }

                    if (!acknowledged)
                    {
                        completed = Release(resend: false);
                    }

                    if (completed is { } colorCommand && NeedsColorReadback(colorCommand))
                    {
                        RefreshColor();
                    }
                    else
                    {
                        lock (_gate)
                        {
                            StartPendingReadback();
                        }
                        Pump();
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
                    ReadColor(packet);
                    break;
            }
        }
    }

    private void ReadColor(byte[] packet)
    {
        lock (_gate)
        {
            if (_inquiry is not { } command || !ViscaPacket.TryReadColor(command.Category, packet, out var value))
            {
                return;
            }

            switch (command.Category)
            {
                case ViscaCategory.WhiteBalanceInquiry:
                    _whiteBalance = value;
                    break;
                case ViscaCategory.RedGainInquiry:
                    _redGain = value;
                    break;
                case ViscaCategory.BlueGainInquiry:
                    _blueGain = value;
                    break;
            }

            if (!_refreshPending && _whiteBalance is { } mode && _redGain is { } red && _blueGain is { } blue)
            {
                _accepted.Remove(ViscaCategory.RedGain);
                _accepted.Remove(ViscaCategory.BlueGain);
                _color.OnNext(new CameraColorState(mode == 0x05, red, blue));
            }

            ReleaseInquiry(resend: false);
        }
    }

    /// A refusal that follows an acknowledgement is reported against the buffer that holds the
    /// command, not against whatever is in flight now, so the buffer is what names it. Any other
    /// refusal is for the command in flight.
    private ViscaCategory? Refuse(int socket, bool resend)
    {
        Command buffered;
        var inquiry = false;
        var ambiguousStop = false;
        lock (_gate)
        {
            if (!_sockets.Remove(socket, out buffered))
            {
                if (_expiredColorSockets.Contains(socket))
                {
                    return ViscaCategory.ColorAction;
                }
                buffered = default;
                // Inquiries use socket 0 and left before anything now in flight. A lost inquiry
                // reply makes that guess unsafe, so a stop in flight is resent with it.
                inquiry = socket == 0 && _inquiry is not null;
                ambiguousStop = inquiry && resend && _inFlight is { } stop && IsMovementStop(stop.Category, stop.Packet);
                if (!inquiry && !resend && _inFlight is { } refused && IsColorCorrection(refused.Category))
                {
                    DiscardColorCommands();
                    _refreshPending = true;
                    _color.OnNext(null);
                }
            }
            else if (buffered.Category == ViscaCategory.ColorAction)
            {
                if (resend)
                {
                    Requeue(buffered);
                }
                else
                {
                    DiscardColorCommands();
                    _refreshPending = true;
                    _color.OnNext(null);
                }
            }
            else if (resend
                && _accepted.TryGetValue(buffered.Category, out var newest)
                && ReferenceEquals(newest, buffered.Packet))
            {
                _accepted.Remove(buffered.Category);
                Requeue(buffered);
            }
            else if (!resend && buffered.Category is ViscaCategory.RedGain or ViscaCategory.BlueGain)
            {
                DiscardColorCommands();
                _refreshPending = true;
                _color.OnNext(null);
            }
            StartPendingReadback();
        }

        if (buffered.Packet is null)
        {
            if (!inquiry)
            {
                return Release(resend)?.Category;
            }
            var refused = ReleaseInquiry(resend);
            if (ambiguousStop)
            {
                Release(resend: true);
            }
            return refused?.Category;
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
                    if (!IsColorCategory(lost.Category))
                    {
                        Requeue(lost);
                    }
                }
                _inquiry = null;
                _inquiryExpiry.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

                _sockets.Clear();
                _expiredColorSockets.Clear();
                _colorCompletionExpiry.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

                // A camera that went away may come back having forgotten everything, so nothing it
                // was told before still counts as sent.
                _accepted.Clear();
                _whiteBalance = null;
                _redGain = null;
                _blueGain = null;
                _refreshPending = false;
                DiscardColorCommands(includeInquiries: true);

                _color.OnNext(null);
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

    private void DiscardColorCommands(bool includeInquiries = false)
    {
        _queue.RemoveAll(command => IsColorCategory(command.Category)
            && (includeInquiries || !ViscaPacket.IsColorInquiry(command.Category)));
    }

    private static bool IsColorCorrection(ViscaCategory category) =>
        category is ViscaCategory.ColorAction or ViscaCategory.RedGain or ViscaCategory.BlueGain;

    private static bool IsColorCategory(ViscaCategory category) =>
        IsColorCorrection(category) || ViscaPacket.IsColorInquiry(category);

    // VISCA cannot move below speed 1, even when the percentage asks for less.
    private double PanTiltMaximum(int maximum) => Math.Max(1, maximum * _config.PanTiltSpeedPercent / 100.0);

    private static int Scale(double value, double maximum) =>
        (int)Math.Round(Math.Clamp(value, -1, 1) * maximum);

    private static bool NeedsColorReadback(Command command) =>
        command is { Category: ViscaCategory.ColorAction, Packet: [0x81, 0x01, 0x04, 0x10, 0x05, 0xFF] }
            or { Category: ViscaCategory.ColorAction, Packet: [0x81, 0x01, 0x04, 0x35, 0x00 or 0x05, 0xFF] };

    private static bool IsMovementStop(ViscaCategory category, byte[] packet) =>
        category == ViscaCategory.PanTilt && packet[6] == 0x03 && packet[7] == 0x03
        || category is ViscaCategory.Zoom or ViscaCategory.Focus && packet[4] == 0;

    private readonly record struct Command(ViscaCategory Category, byte[] Packet, long SentAt);
}
