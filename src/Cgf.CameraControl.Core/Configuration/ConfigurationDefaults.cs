using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Cgf.CameraControl.Core.Configuration;

/// Leaves out of the file every optional property that still holds the value a new record starts
/// with, so a hand-written file that omits its defaults is written back without them rather than
/// gaining a line for every setting nobody changed.
///
/// The default is read off a record the contract creates, and compared as the JSON both values
/// would be written as, so a dictionary or a nested record that is empty counts as its default too.
/// Required properties are always written: leaving one out would make the file unreadable.
internal static class ConfigurationDefaults
{
    public static void OmitWhenDefault(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object || info.CreateObject is not { } create)
        {
            return;
        }

        var defaults = new Lazy<object>(create);
        foreach (var property in info.Properties)
        {
            if (property.IsRequired || property.Get is not { } get)
            {
                continue;
            }

            var options = info.Options;
            property.ShouldSerialize = (_, value) => !Same(value, get(defaults.Value), property.PropertyType, options);
        }
    }

    private static bool Same(object? value, object? fallback, Type type, JsonSerializerOptions options)
    {
        if (Equals(value, fallback))
        {
            return true;
        }

        if (value is null || fallback is null)
        {
            return false;
        }

        var typeInfo = options.GetTypeInfo(type);
        return JsonSerializer.Serialize(value, typeInfo) == JsonSerializer.Serialize(fallback, typeInfo);
    }
}
