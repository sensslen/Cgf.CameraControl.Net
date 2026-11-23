using System.Buffers;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Scenes;

namespace Cgf.CameraControl.App.Configuration;

public sealed class ConfigFormatException(string message) : Exception(message);

/// Reads a configuration into its typed records, one entry at a time, so an entry that is malformed
/// or holds a value its record's rules refuse is reported on its own instead of failing the file, and
/// is kept as an invalid entry rather than dropped, so saving the file does not lose it.
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
                    (type, json, problem) => new InvalidInterfaceConfiguration(type, json, problem)))
            {
                Scenes = ReadScenes(root, collected),
            };

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
                    var read = Read(element, type!, typeInfo);
                    var refused = read.Validate().Select(value => $"{read}.{value.Path}: {value.Message}").ToList();
                    if (refused.Count == 0)
                    {
                        entries.Add(read);
                        continue;
                    }

                    problem = $"{position}: {string.Join("; ", refused)}";
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

    private static List<CameraScene> ReadScenes(JsonElement root, List<string> issues)
    {
        var scenes = new List<CameraScene>();
        if (!root.TryGetProperty("scenes", out var section))
        {
            return scenes;
        }

        if (section.ValueKind != JsonValueKind.Array)
        {
            issues.Add($"'scenes' must be an array, found {section.ValueKind}");
            return scenes;
        }

        var index = 0;
        foreach (var element in section.EnumerateArray())
        {
            var position = $"scenes[{index++}]";
            try
            {
                var scene = ReadScene(element, position);
                var refused = scene.Validate().Select(problem => $"{position}.{problem.Path}: {problem.Message}").ToList();
                if (refused.Count != 0)
                {
                    issues.Add(string.Join("; ", refused));
                    continue;
                }

                if (scenes.Any(existing => CameraScene.SameName(existing.Name, scene.Name)))
                {
                    throw new ConfigValidationException($"{position}.name", $"another scene is already named {scene.Name}");
                }

                scenes.Add(scene);
            }
            catch (ConfigValidationException ex)
            {
                issues.Add(ex.Message);
            }
        }

        return scenes;
    }

    private static CameraScene ReadScene(JsonElement element, string position)
    {
        CameraScene? scene;
        try
        {
            scene = element.Deserialize(ConfigurationContext.Default.CameraScene);
        }
        catch (JsonException ex)
        {
            throw new ConfigValidationException(Locate(position, ex.Path), Explain(ex.Message));
        }

        if (scene is null)
        {
            throw new ConfigValidationException(position, "is empty");
        }

        // The keys are read as numbers, so "1" and "01" would land on one camera without an error.
        var listed = new HashSet<int>();
        foreach (var camera in element.GetProperty("cameras").EnumerateObject())
        {
            var instance = int.Parse(camera.Name, CultureInfo.InvariantCulture);
            if (!listed.Add(instance))
            {
                throw new ConfigValidationException($"{position}.cameras.{camera.Name}", $"camera {instance} is listed twice");
            }
        }

        return scene with { Name = scene.Name.Trim() };
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
