using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration.Converters;

namespace Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;

/// websocketPtzLancCameraConfigurationSchema: configSchema.extend({
///   ip: z.string(),
///   panTiltInvert: z.boolean().default(false).optional(),
///   showTallyLight: z.boolean().default(true).optional() })
/// Two System.Text.Json rules shape how every configuration record in this solution is written, and
/// breaking either one produces wrong values rather than an error.
///
/// Properties are declared `get; set;`, never `get; init;`. The source generator does not run
/// property initialisers for init-only properties, so `ShowTallyLight = true` would silently arrive
/// as false. Reflection-based serialization does honour them, so this only shows up once published.
///
/// Presence is enforced with [JsonRequired], never the C# `required` modifier. A type carrying a
/// required member cannot be constructed with `new T()`, which forces the same uninitialized path
/// and loses the defaults again.
public sealed record WebsocketPtzLancCameraConfiguration
{
    [JsonRequired]
    [JsonConverter(typeof(HostConverter))]
    public string Ip { get; set; } = null!;

    public bool PanTiltInvert { get; set; }

    public bool ShowTallyLight { get; set; } = true;
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(WebsocketPtzLancCameraConfiguration))]
public sealed partial class WebsocketPtzLancCameraConfigurationContext : JsonSerializerContext;
