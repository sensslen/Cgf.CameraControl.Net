using Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;

namespace Cgf.CameraControl.Cameras.SignalrPtzLanc;

// The SignalR hub protocol serializes whatever it is handed by looking the type up in its resolver
// chain. Under NativeAOT there is no reflection based resolver behind the generated one, so a state
// type that stopped being generated leaves a camera that connects, reports itself healthy, and then
// fails every single update. This drives the real protocol over the real payload options.
public static class AotProbe
{
    public static (bool Alive, string Report) Touch()
    {
        var options = new JsonHubProtocolOptions();
        SignalrStateTransport.ConfigurePayload(options.PayloadSerializerOptions);
        var protocol = new JsonHubProtocol(Options.Create(options));

        string written;
        try
        {
            var state = new SignalrPtzLancCameraState { Pan = 255, Tilt = -255, Zoom = 8, Focus = 1 };
            written = System.Text.Encoding.UTF8.GetString(protocol.GetMessageBytes(new InvocationMessage("SetState", [state])).ToArray());
        }
        catch (Exception ex)
        {
            return (false, $"signalr SetState -> FAIL: {ex.GetType().Name}: {ex.Message}");
        }

        // The camelCase names are what the controller reads; a payload of an empty object means the
        // members were trimmed away rather than serialized.
        var alive = written.Contains("\"pan\":255") && written.Contains("\"focus\":1");
        return alive
            ? (true, "signalr SetState -> OK: the state payload still serializes")
            : (false, $"signalr SetState -> FAIL: the payload lost its members: {written}");
    }
}
