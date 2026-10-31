using System.Text.Json;

namespace Cgf.CameraControl.Core.Configuration;

public sealed class ConfigFormatException(string message) : Exception(message);

// Parsed with JsonDocument rather than a deserializer so the whole path stays reflection free for
// NativeAOT, and so an entry that is malformed can be reported on its own instead of failing the file.
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
            var config = new RootConfig(
                ReadSection(document.RootElement, "cams", collected),
                ReadSection(document.RootElement, "videoMixers", collected),
                ReadSection(document.RootElement, "interfaces", collected));

            issues = collected;
            return config;
        }
    }

    public static RootConfig LoadFile(string path, out IReadOnlyList<string> issues) =>
        Load(File.ReadAllText(path), out issues);

    private static List<ConfigEntry> ReadSection(JsonElement root, string name, List<string> issues)
    {
        var entries = new List<ConfigEntry>();
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

            if (element.ValueKind != JsonValueKind.Object)
            {
                issues.Add($"{position} must be an object, found {element.ValueKind}");
                continue;
            }

            if (!element.TryGetProperty("instance", out var instance) || !instance.TryGetInt32(out var instanceNumber))
            {
                issues.Add($"{position} is missing a numeric 'instance'");
                continue;
            }

            if (!element.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
            {
                issues.Add($"{position} is missing a string 'type'");
                continue;
            }

            entries.Add(new ConfigEntry(instanceNumber, type.GetString()!, element.Clone()));
        }

        return entries;
    }
}
