using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration;
using Microsoft.Extensions.Options;

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
    [Required]
    [Host]
    public string Ip { get; set; } = string.Empty;

    public bool? PanTiltInvert { get; set; }

    [Range(1, 100)]
    public int? PanTiltSpeedPercent { get; set; }

    public bool? ShowTallyLight { get; set; }

    public override IEnumerable<ValueProblem> Validate() => ValueProblem.Of(new WebsocketPtzLancCameraConfigurationValidator(), this);
}

[OptionsValidator]
internal sealed partial class WebsocketPtzLancCameraConfigurationValidator : IValidateOptions<WebsocketPtzLancCameraConfiguration>;
