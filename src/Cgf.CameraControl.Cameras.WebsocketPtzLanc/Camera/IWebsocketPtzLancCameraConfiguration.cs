using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration.Converters;

namespace Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;

/// websocketPtzLancCameraConfigurationSchema: configSchema.extend({
///   ip: z.string(),
///   panTiltInvert: z.boolean().default(false).optional(),
///   showTallyLight: z.boolean().default(true).optional() })
public sealed record WebsocketPtzLancCameraConfiguration
{
    [JsonConverter(typeof(HostConverter))]
    public required string Ip { get; init; }

    public bool PanTiltInvert { get; init; }

    public bool ShowTallyLight { get; init; } = true;
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(WebsocketPtzLancCameraConfiguration))]
public sealed partial class WebsocketPtzLancCameraConfigurationContext : JsonSerializerContext;
