using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Cgf.CameraControl.Core.Configuration;

/// Writes a configuration back out as the three arrays it was read from, each entry from its typed
/// record with `type` first and its unchanged defaults left out. An entry the loader could not read
/// is written from the JSON it arrived as, so nothing in the file is lost by opening and saving it.
///
/// Comments do not come back. The loader parses with them skipped, so they are not in memory to write.
public static class ConfigWriter
{
    public static string Write(RootConfig config, ConfigurationSchema schema)
    {
        var buffer = new ArrayBufferWriter<byte>();

        // Not the platform's newline, so the same desk written on a Mac and on Windows differs where
        // it was edited rather than on every line.
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
        {
            writer.WriteStartObject();
            Section(writer, "cams", config.Cams, schema);
            Section(writer, "videoMixers", config.VideoMixers, schema);
            Section(writer, "interfaces", config.Interfaces, schema);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }

    public static void WriteFile(string path, RootConfig config, ConfigurationSchema schema) =>
        File.WriteAllText(path, Write(config, schema));

    private static void Section<T>(Utf8JsonWriter writer, string name, IReadOnlyList<T> entries, ConfigurationSchema schema)
        where T : EntryConfiguration
    {
        var typeInfo = schema.Options.GetTypeInfo(typeof(T));
        writer.WriteStartArray(name);
        foreach (var entry in entries)
        {
            if (entry is IInvalidConfiguration invalid)
            {
                using var document = JsonDocument.Parse(invalid.Json);
                document.RootElement.WriteTo(writer);
            }
            else
            {
                JsonSerializer.Serialize(writer, entry, typeInfo);
            }
        }

        writer.WriteEndArray();
    }
}
