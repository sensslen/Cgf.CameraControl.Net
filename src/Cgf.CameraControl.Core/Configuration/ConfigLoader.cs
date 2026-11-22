using System.Buffers;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Cgf.CameraControl.Core.Configuration;

public sealed class ConfigFormatException(string message) : Exception(message);

/// Reads a configuration into its typed records, one entry at a time, so an entry that is malformed
/// is reported on its own instead of failing the file, and is kept as an invalid entry rather than
/// dropped, so saving the file does not lose it.
public static class ConfigLoader
{
    public static RootConfig Load(string json, ConfigurationSchema schema, out IReadOnlyList<string> issues)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException ex)
        {
            throw new ConfigFormatException($"the configuration is not valid JSON: {ex.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ConfigFormatException($"the configuration must be a JSON object, found {document.RootElement.ValueKind}");
            }

            var collected = new List<string>();
            var root = document.RootElement;
            var config = new RootConfig(
                ReadSection<CameraConfiguration>(root, "cams", schema, collected,
                    (type, json, problem) => new InvalidCameraConfiguration(type, json, problem)),
                ReadSection<VideoMixerConfiguration>(root, "videoMixers", schema, collected,
                    (type, json, problem) => new InvalidVideoMixerConfiguration(type, json, problem)),
                ReadSection<InterfaceConfiguration>(root, "interfaces", schema, collected,
                    (type, json, problem) => new InvalidInterfaceConfiguration(type, json, problem)));

            issues = collected;
            return config;
        }
    }

    public static RootConfig LoadFile(string path, ConfigurationSchema schema, out IReadOnlyList<string> issues) =>
        Load(File.ReadAllText(path), schema, out issues);

    private static List<T> ReadSection<T>(
        JsonElement root,
        string name,
        ConfigurationSchema schema,
        List<string> issues,
        Func<string, string, string, T> invalid)
        where T : EntryConfiguration
    {
        var entries = new List<T>();
        if (!root.TryGetProperty(name, out var section))
        {
            return entries;
        }

        if (section.ValueKind != JsonValueKind.Array)
        {
            issues.Add($"'{name}' must be an array, found {section.ValueKind}");
            return entries;
        }

        var types = schema.TypesOf<T>();
        var index = 0;
        foreach (var element in section.EnumerateArray())
        {
            var position = $"{name}[{index++}]";
            var type = element.ValueKind == JsonValueKind.Object &&
                       element.TryGetProperty("type", out var named) &&
                       named.ValueKind == JsonValueKind.String
                ? named.GetString()!
                : null;

            var problem = Problem(element, type, types);
            if (problem is null)
            {
                try
                {
                    entries.Add(Read(element, types.First(known => string.Equals(known, type, StringComparison.OrdinalIgnoreCase)),
                        (JsonTypeInfo<T>)schema.Options.GetTypeInfo(typeof(T))));
                    continue;
                }
                catch (ConfigValidationException ex)
                {
                    problem = $"{position}: {ex.Message}";
                }
            }
            else
            {
                problem = $"{position} {problem}";
            }

            issues.Add(problem);
            var entry = invalid(type ?? string.Empty, Text(element), problem);
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty("instance", out var instance) &&
                instance.TryGetInt32(out var number))
            {
                entry.Instance = number;
            }

            entries.Add(entry);
        }

        return entries;
    }

    /// What keeps an entry from being read at all, before its type gets to say anything about it.
    private static string? Problem(JsonElement element, string? type, IReadOnlyList<string> types)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return $"must be an object, found {element.ValueKind}";
        }

        if (!element.TryGetProperty("instance", out var instance) || !instance.TryGetInt32(out _))
        {
            return "is missing a numeric 'instance'";
        }

        if (type is null)
        {
            return "is missing a string 'type'";
        }

        return types.Any(known => string.Equals(known, type, StringComparison.OrdinalIgnoreCase))
            ? null
            : $"has the type '{type}', which nothing claims. Known types: {string.Join(", ", types.Order(StringComparer.Ordinal))}";
    }

    /// Type strings are matched without regard to case, which a discriminator is not, so the entry is
    /// read with the registered spelling written in place of its own. Rewriting it also drops the
    /// comments and trailing commas the document skipped, which a reader over the element would not.
    private static T Read<T>(JsonElement element, string type, JsonTypeInfo<T> typeInfo)
        where T : EntryConfiguration
    {
        var label = $"{type}[{element.GetProperty("instance").GetInt32().ToString(CultureInfo.InvariantCulture)}]";
        try
        {
            return JsonSerializer.Deserialize(WithType(element, type), typeInfo) ?? throw new ConfigValidationException(label, "is empty");
        }
        catch (JsonException ex)
        {
            throw new ConfigValidationException(Locate(label, ex.Path), Explain(ex.Message));
        }
        catch (NotSupportedException ex)
        {
            // An absent polymorphic discriminator, such as a connectionChange with no type, arrives
            // as NotSupportedException rather than JsonException. Without this the framework's own
            // message escapes naming no entry, and the operator is told a CLR type is unsupported
            // instead of which line of their file is wrong.
            throw new ConfigValidationException(Locate(label, PathIn(ex.Message)), Explain(ex.Message));
        }
    }

    private static byte[] WithType(JsonElement element, string type)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("type"))
                {
                    writer.WriteString("type", type);
                }
                else
                {
                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// The entry as JSON without the comments the file may have carried inside it.
    private static string Text(JsonElement element)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            element.WriteTo(writer);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string Locate(string label, string? path) =>
        string.IsNullOrEmpty(path) ? label : $"{label}{path.TrimStart('$')}";

    // The message repeats the path and line info the caller already prints.
    private static string Explain(string message)
    {
        var cut = message.IndexOf(" Path: ", StringComparison.Ordinal);
        return cut < 0 ? message : message[..cut];
    }

    // NotSupportedException carries no Path property, only the path appended to its message.
    private static string? PathIn(string message)
    {
        var start = message.IndexOf(" Path: ", StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += " Path: ".Length;
        var end = message.IndexOf(" | ", start, StringComparison.Ordinal);
        return end < 0 ? message[start..] : message[start..end];
    }
}
