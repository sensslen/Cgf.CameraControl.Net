using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cgf.CameraControl.Core.Configuration.Converters;

/// Tightens the plain z.string() the TypeScript schemas use for device addresses. An IPv4 or IPv6
/// literal or a DNS host name is accepted; anything else is reported against the field that holds it
/// instead of surfacing later as a connection that never comes up.
public sealed class HostConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("expected a host name or IP address, found an empty string");
        }

        if (!IPAddress.TryParse(value, out _) && Uri.CheckHostName(value) == UriHostNameType.Unknown)
        {
            throw new JsonException($"expected a host name or IP address, found '{value}'");
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
