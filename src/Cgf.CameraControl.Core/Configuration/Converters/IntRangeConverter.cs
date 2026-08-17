using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cgf.CameraControl.Core.Configuration.Converters;

/// The zod integer refinements, one converter per rule. A parameterised JsonConverterAttribute would
/// read better but the source generator rejects those (SYSLIB1223), so each rule is its own type.
/// Throwing JsonException while reading lets System.Text.Json attach the JSON path.
public abstract class IntRangeConverter(int minimum, int maximum) : JsonConverter<int>
{
    public sealed override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetInt32();
        return value >= minimum && value <= maximum ? value : throw new JsonException(Describe(value));
    }

    public sealed override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);

    private string Describe(int value) => (minimum, maximum) switch
    {
        (0, int.MaxValue) => $"expected a non-negative integer, found {value}",
        (1, int.MaxValue) => $"expected a positive integer, found {value}",
        _ => $"expected an integer in [{minimum} .. {maximum}], found {value}",
    };
}

/// z.int().nonnegative()
public sealed class NonNegativeIntConverter() : IntRangeConverter(0, int.MaxValue);

/// z.int().positive()
public sealed class PositiveIntConverter() : IntRangeConverter(1, int.MaxValue);

/// z.int().positive() constrained to a usable TCP or UDP port
public sealed class PortConverter() : IntRangeConverter(1, 65535);
