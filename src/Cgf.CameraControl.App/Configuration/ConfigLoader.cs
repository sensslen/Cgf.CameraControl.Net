using System.Buffers;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cgf.CameraControl.Core.Configuration;

namespace Cgf.CameraControl.App.Configuration;

public sealed class ConfigFormatException(string message) : Exception(message);

/// Reads a configuration into its typed records, one entry at a time, so an entry that is malformed
/// is reported on its own instead of failing the file, and is kept as an invalid entry rather than
/// dropped, so saving the file does not lose it.
public static class ConfigLoader
{
    public static RootConfig Load(string json, out IReadOnlyList<string> issues)
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
                ReadSection(root, "cams", ConfigurationSections.Cameras, collected,
                    (type, json, problem) => new InvalidCameraConfiguration(type, json, problem)),
                ReadSection(root, "videoMixers", ConfigurationSections.VideoMixers, collected,
                    (type, json, problem) => new InvalidVideoMixerConfiguration(type, json, problem)),
                ReadSection(root, "interfaces", ConfigurationSections.Interfaces, collected,
                    (type, json, problem) => new InvalidInterfaceConfiguration(type, json, problem)));

            issues = collected;
            return config;
        }
    }

    public static RootConfig LoadFile(string path, out IReadOnlyList<string> issues) =>
        Load(File.ReadAllText(path), out issues);

    private static List<T> ReadSection<T>(
        JsonElement root,
        string name,
        JsonTypeInfo<T> typeInfo,
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

        var index = 0;
        foreach (var element in section.EnumerateArray())
        {
            var position = $"{name}[{index++}]";
            var type = element.ValueKind == JsonValueKind.Object &&
                       element.TryGetProperty("type", out var named) &&
                       named.ValueKind == JsonValueKind.String
                ? named.GetString()!
                : null;

            var problem = Problem(element, type);
            if (problem is null)
            {
                try
                {
                    entries.Add(Read(element, type!, typeInfo));
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

    /// What keeps an entry from being named in an error at all.
    private static string? Problem(JsonElement element, string? type)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return $"must be an object, found {element.ValueKind}";
        }

        if (!element.TryGetProperty("instance", out var instance) || !instance.TryGetInt32(out _))
        {
            return "is missing a numeric 'instance'";
        }

        return type is null ? "is missing a string 'type'" : null;
    }

    private static T Read<T>(JsonElement element, string type, JsonTypeInfo<T> typeInfo)
        where T : EntryConfiguration
    {
        var label = $"{type}[{element.GetProperty("instance").GetInt32().ToString(CultureInfo.InvariantCulture)}]";
        try
        {
            return element.Deserialize(typeInfo) ?? throw new ConfigValidationException(label, "is empty");
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
