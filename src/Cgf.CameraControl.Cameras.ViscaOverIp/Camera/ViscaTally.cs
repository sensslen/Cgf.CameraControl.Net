using Cgf.CameraControl.Core.CameraConnection;

namespace Cgf.CameraControl.Cameras.ViscaOverIp.Camera;

/// Vendor specific tally payloads, sent verbatim. None of these is in the VISCA specification, so
/// there is nothing to derive them from and a camera told the wrong vendor's payload does something
/// unrelated rather than nothing.
public static class ViscaTally
{
    public static byte[]? Payload(ViscaTallyMode mode, TallyState state) => mode switch
    {
        // Both lamps in one packet, which is what makes this a mode: Sony spends a command per lamp.
        // CineTreak support confirms the CT-PT31K on these bytes, so the name is narrower than the
        // cameras that answer to it.
        ViscaTallyMode.Avonic => state switch
        {
            TallyState.Program => [0x81, 0x01, 0x7E, 0x01, 0x0A, 0x00, 0x02, 0x03, 0xFF],
            TallyState.Preview => [0x81, 0x01, 0x7E, 0x01, 0x0A, 0x00, 0x03, 0x02, 0xFF],
            _ => [0x81, 0x01, 0x7E, 0x01, 0x0A, 0x00, 0x03, 0x03, 0xFF],
        },
        // This one has a lamp rather than a colour, so preview leaves it dark.
        ViscaTallyMode.PtzOptics => state switch
        {
            TallyState.Program => [0x81, 0x0A, 0x02, 0x02, 0x02, 0xFF],
            _ => [0x81, 0x0A, 0x02, 0x02, 0x03, 0xFF],
        },
        _ => null,
    };
}
