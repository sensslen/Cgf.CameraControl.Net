using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;

/// signalrPtzLancCameraConfigurationSchema: configSchema.extend({
///   connectionUrl: z.string(),
///   connectionPort: z.string(),
///   panTiltInvert: z.boolean().default(false).optional() })
/// On the `get; set;` and [JsonRequired] conventions, see EntryConfiguration.
public sealed record SignalrPtzLancCameraConfiguration() : CameraConfiguration(TypeName)
{
    public const string TypeName = "Signalr.PtzLanc";

    /// Paths are appended to it, so a trailing slash would double up.
    [JsonRequired]
    [Required]
    [HttpUrl]
    public string ConnectionUrl { get; set => field = value.TrimEnd('/'); } = string.Empty;

    /// The serial port the controller drives the camera through, named as that controller names it:
    /// "COM6" on Windows, "/dev/ttyUSB0" elsewhere. One controller can hold several.
    [JsonRequired]
    [Required]
    public string ConnectionPort { get; set; } = string.Empty;

    public bool? PanTiltInvert { get; set; }

    [Range(1, 100)]
    public int? PanTiltSpeedPercent { get; set; }

    public override IEnumerable<ValueProblem> Validate() => ValueProblem.Of(new SignalrPtzLancCameraConfigurationValidator(), this);
}

[OptionsValidator]
internal sealed partial class SignalrPtzLancCameraConfigurationValidator : IValidateOptions<SignalrPtzLancCameraConfiguration>;
