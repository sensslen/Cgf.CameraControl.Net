using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration.Converters;

namespace Cgf.CameraControl.Cameras.ViscaOverIp.Camera;

/// The tally lamp is not part of the VISCA specification, so every vendor spells it differently and
/// the payloads are only interchangeable by accident. Naming the vendor is what picks the payload.
public enum ViscaTallyMode
{
    [JsonStringEnumMemberName("none")]
    None,

    [JsonStringEnumMemberName("sony-lumens")]
    SonyLumens,

    [JsonStringEnumMemberName("avonic")]
    Avonic,

    [JsonStringEnumMemberName("ptzoptics")]
    PtzOptics,
}

/// viscaOverIpCameraConfigurationSchema: configSchema.extend({
///   ip: z.string(),
///   port: z.number().default(52381).optional(),
///   panTiltInvert: z.boolean().default(false).optional(),
///   tallyMode: z.enum(['none', 'sony-lumens', 'avonic', 'ptzoptics']).default('none').optional() })
/// On the `get; set;` and [JsonRequired] conventions, see WebsocketPtzLancCameraConfiguration.
public sealed record ViscaOverIpCameraConfiguration
{
    [JsonRequired]
    [JsonConverter(typeof(HostConverter))]
    public string Ip { get; set; } = null!;

    [JsonConverter(typeof(PortConverter))]
    public int Port { get; set; } = 52381;

    public bool PanTiltInvert { get; set; }

    public ViscaTallyMode TallyMode { get; set; } = ViscaTallyMode.None;
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ViscaOverIpCameraConfiguration))]
public sealed partial class ViscaOverIpCameraConfigurationContext : JsonSerializerContext;
