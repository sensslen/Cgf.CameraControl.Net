using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Scenes;

namespace Cgf.CameraControl.App.Configuration;

/// Writes a configuration back out as the arrays it was read from, each entry from its typed
/// record with `type` first and its unset settings left out. An entry the loader could not read is
/// written from the JSON it arrived as. A scene the loader could not read is left out.
///
/// Comments do not come back. The loader parses with them skipped, so they are not in memory to write.
public static class ConfigWriter
{
    public static string Write(RootConfig config)
    {
        var buffer = new ArrayBufferWriter<byte>();

        // Not the platform's newline, so the same desk written on a Mac and on Windows differs where
        // it was edited rather than on every line.
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
        {
            writer.WriteStartObject();
            Section(writer, "cams", config.Cams, ConfigurationSections.Cameras);
            Section(writer, "videoMixers", config.VideoMixers, ConfigurationSections.VideoMixers);
            Section(writer, "interfaces", config.Interfaces, ConfigurationSections.Interfaces);
            if (config.Scenes.Count != 0)
            {
                writer.WriteStartArray("scenes");
                foreach (var scene in config.Scenes)
                {
                    JsonSerializer.Serialize(
                        writer,
                        scene with { Cameras = new SortedDictionary<int, ScenePosition>(scene.Cameras.ToDictionary()) },
                        ConfigurationContext.Default.CameraScene);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }

    public static void WriteFile(string path, RootConfig config) =>
        File.WriteAllText(path, Write(config));

    private static void Section<T>(Utf8JsonWriter writer, string name, IReadOnlyList<T> entries, JsonTypeInfo<T> typeInfo)
        where T : EntryConfiguration
    {
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
