using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;

/// Tightens the plain z.string() the TypeScript schema uses for the controller address. The value is
/// concatenated with a path and handed to an HTTP client, so a relative or malformed URL has to be
/// reported against the field that holds it rather than as a request that never comes back.
public sealed class ControllerUrlConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString()?.TrimEnd('/');
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            throw new JsonException($"expected an http or https URL, found '{value}'");
        }

        return value!;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}

/// signalrPtzLancCameraConfigurationSchema: configSchema.extend({
///   connectionUrl: z.string(),
///   connectionPort: z.string(),
///   panTiltInvert: z.boolean().default(false).optional() })
/// On the `get; set;` and [JsonRequired] conventions, see WebsocketPtzLancCameraConfiguration.
public sealed record SignalrPtzLancCameraConfiguration
{
    [JsonRequired]
    [JsonConverter(typeof(ControllerUrlConverter))]
    public string ConnectionUrl { get; set; } = null!;

    /// The serial port the controller drives the camera through, named as that controller names it:
    /// "COM6" on Windows, "/dev/ttyUSB0" elsewhere. One controller can hold several.
    [JsonRequired]
    public string ConnectionPort { get; set; } = null!;

    public bool PanTiltInvert { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(SignalrPtzLancCameraConfiguration))]
public sealed partial class SignalrPtzLancCameraConfigurationContext : JsonSerializerContext;
