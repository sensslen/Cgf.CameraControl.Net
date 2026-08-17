using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cgf.CameraControl.Core.Configuration;

public static class ConfigJson
{
    /// Property names are camelCase to match the configuration files the TypeScript build reads and
    /// writes. Enums round trip as their string names: numbers are accepted on the way in, but never
    /// produced on the way out, so an exported file stays loadable by the TypeScript zod schemas.
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        WriteIndented = true,
    };
}
