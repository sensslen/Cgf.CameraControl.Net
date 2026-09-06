namespace Cgf.CameraControl.Cameras.ViscaOverIp.Camera;

/// What the camera is told to do. One packet per category is kept, so a stick that is moved while
/// the previous movement is still unacknowledged replaces it rather than queueing behind it.
public enum ViscaCategory
{
    PanTilt,
    Zoom,
    Focus,
    Tally,
}

/// How the camera answered.
public enum ViscaReply
{
    /// Anything that does not settle a command: a completion for an inquiry we never sent, a network
    /// change notice, or a packet we could not read.
    Other,
    Acknowledged,
    Completed,
    Failed,
}

/// The VISCA packets this camera sends, built here rather than taken from a library.
///
/// node-visca-over-ip, which the TypeScript camera uses, writes raw VISCA to the socket with no
/// Sony VISCA-over-IP header, and this port keeps that on the wire so the same camera answers both
/// implementations. It does not keep that library's variable speed bug: it shifts the direction
/// byte left by eight rather than four, and JavaScript then truncates the direction away, so every
/// variable speed zoom and focus goes out as a bare speed the camera reads as a different command.
/// The bytes below are what the VISCA specification asks for.
public static class ViscaPacket
{
    private const byte Terminator = 0xFF;

    // Address 1. Every camera this talks to is addressed directly rather than daisy chained.
    private const byte Header = 0x81;
    private const byte Command = 0x01;
    private const byte CategoryCamera = 0x04;
    private const byte CategoryPanTilt = 0x06;

    public const int MaximumPanSpeed = 0x18;
    public const int MaximumTiltSpeed = 0x14;

    /// The lens speed the camera accepts is p = 0 (low) to 7 (high), and the stop is the whole byte
    /// rather than a speed of zero. Counting the steps rather than naming the highest one is what
    /// keeps the slowest of them reachable: pan and tilt have no speed zero, the lens does, and
    /// treating it like an axis loses the only zoom slow enough to frame a shot with.
    public const int LensSpeeds = 8;

    /// 8x 01 06 01 VV WW XX YY FF, where XX is 1 left, 2 right, 3 stop and YY is 1 up, 2 down,
    /// 3 stop. A speed byte of zero is not a stop, so a stopped axis still carries a legal speed.
    public static byte[] PanTilt(int pan, int tilt) =>
    [
        Header, Command, CategoryPanTilt, 0x01,
        (byte)Math.Max(1, Math.Min(MaximumPanSpeed, Math.Abs(pan))),
        (byte)Math.Max(1, Math.Min(MaximumTiltSpeed, Math.Abs(tilt))),
        Direction(pan, negative: 0x01, positive: 0x02),
        Direction(tilt, negative: 0x02, positive: 0x01),
        Terminator,
    ];

    /// 8x 09 00 02 FF, the version inquiry. Every VISCA camera answers it and nothing about the
    /// answer matters: that one arrived at all is what says a camera is on the other end of a socket
    /// that would look exactly the same pointed at an empty address.
    public static byte[] Presence() => [Header, 0x09, 0x00, 0x02, Terminator];

    /// 8x 01 04 07 pp FF: 00 stop, 2p tele, 3p wide, over speed steps 1 to LensSpeeds.
    public static byte[] Zoom(int speed) => Lens(0x07, speed);

    /// 8x 01 04 08 pp FF: 00 stop, 2p far, 3p near, over speed steps 1 to LensSpeeds.
    public static byte[] Focus(int speed) => Lens(0x08, speed);

    /// An inquiry is answered with a completion that carries the answer after it, so length is what
    /// separates "the command in buffer y has run" from "here is the camera's version". Reading the
    /// second as the first would open the command gate for a command nobody has answered for.
    public static ViscaReply Classify(byte[] packet) =>
        packet.Length < 3 || packet[^1] != Terminator
            ? ViscaReply.Other
            : (packet[1] & 0xF0) switch
            {
                0x40 => ViscaReply.Acknowledged,
                0x50 => packet.Length == 3 ? ViscaReply.Completed : ViscaReply.Other,
                0x60 => ViscaReply.Failed,
                _ => ViscaReply.Other,
            };

    /// A camera answers each command in its own datagram, but nothing stops it coalescing two, so
    /// the terminator rather than the datagram boundary ends a packet.
    public static IEnumerable<byte[]> Split(byte[] datagram)
    {
        var start = 0;
        for (var index = 0; index < datagram.Length; index++)
        {
            if (datagram[index] != Terminator)
            {
                continue;
            }

            yield return datagram[start..(index + 1)];
            start = index + 1;
        }
    }

    /// The error byte of an 8x 6y ss FF reply.
    public static string Describe(byte[] failure) => failure.Length < 3
        ? "the camera reported an error"
        : failure[2] switch
        {
            0x01 => "message length error",
            0x02 => "syntax error",
            0x03 => "command buffer full",
            0x04 => "command cancelled",
            0x05 => "no socket",
            0x41 => "command not executable",
            var other => $"unknown error 0x{other:X2}",
        };

    private static byte[] Lens(byte operation, int speed)
    {
        var magnitude = Math.Min(LensSpeeds, Math.Abs(speed));
        byte instruction = magnitude == 0
            ? (byte)0x00
            : (byte)((speed > 0 ? 0x20 : 0x30) | (magnitude - 1));

        return [Header, Command, CategoryCamera, operation, instruction, Terminator];
    }

    private static byte Direction(int speed, byte negative, byte positive) =>
        speed == 0 ? (byte)0x03 : speed < 0 ? negative : positive;
}
