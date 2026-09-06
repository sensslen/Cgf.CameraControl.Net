using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration.Converters;

namespace Cgf.CameraControl.Cameras.ViscaOverIp.Camera;

/// The tally lamp is not part of the VISCA specification, so every vendor spells it differently and
/// the payloads are only interchangeable by accident. Naming the vendor is what picks the payload.
public enum ViscaTallyMode
{
    [JsonStringEnumMemberName("none")]
    None,

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
/// 'sony-lumens' is gone. Sony drives its two lamps from two addresses, 8x 01 7E 01 0A 00 0p FF for
/// red and 8x 01 7E 04 1A 00 0p FF for green, and extinguishes a lamp that is not told to stay on
/// every 15 seconds, so it is a mode with a shape none of these payloads have. No Lumens document
/// carries a tally command at all.
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
