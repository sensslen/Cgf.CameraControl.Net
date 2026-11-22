using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Configuration.Converters;

namespace Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;

/// websocketPtzLancCameraConfigurationSchema: configSchema.extend({
///   ip: z.string(),
///   panTiltInvert: z.boolean().default(false).optional(),
///   showTallyLight: z.boolean().default(true).optional() })
/// On the `get; set;` and [JsonRequired] conventions, see EntryConfiguration.
public sealed record WebsocketPtzLancCameraConfiguration() : CameraConfiguration(TypeName)
{
    public const string TypeName = "Websocket.PtzLanc";

    [JsonRequired]
    [JsonConverter(typeof(HostConverter))]
    public string Ip { get; set; } = string.Empty;

    public bool PanTiltInvert { get; set; }

    [JsonConverter(typeof(PercentConverter))]
    public int PanTiltSpeedPercent { get; set; } = 100;

    public bool ShowTallyLight { get; set; } = true;
}
