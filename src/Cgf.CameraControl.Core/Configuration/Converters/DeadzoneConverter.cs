using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cgf.CameraControl.Core.Configuration.Converters;

/// The fraction of stick travel ignored around centre. The upper bound is what keeps the rescaling
/// that follows it meaningful: half the travel is already an unusable stick, and a deadzone of one
/// would leave no travel at all.
public sealed class DeadzoneConverter : JsonConverter<double>
{
    private const double Maximum = 0.5;

    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDouble();
        return value >= 0 && value <= Maximum
            ? value
            : throw new JsonException($"expected a deadzone in [0 .. {Maximum}], found {value}");
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}
