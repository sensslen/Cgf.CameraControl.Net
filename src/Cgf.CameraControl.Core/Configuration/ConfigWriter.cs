using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cgf.CameraControl.Core.Configuration;

/// Writes a configuration back out as the three arrays it was read from.
///
/// Every entry is written from the JSON it carries rather than from a deserialized shape, so an entry
/// nothing edited comes back exactly as it went in, and one whose type nothing claims survives a round
/// trip it was never parsed for. An editor changes an entry by replacing that JSON, which is the only
/// thing this has to know about editing.
///
/// What the loader skipped comes back too: keys other than the three sections, and section entries
/// it rejected, after the entries it took. Saving is not how an operator finds out the loader dropped
/// their line.
///
/// Comments do not come back. The loader parses with them skipped, so they are not in memory to write.
public static class ConfigWriter
{
    private static readonly string[] Sections = ["cams", "videoMixers", "interfaces"];

    public static string Write(RootConfig config)
    {
        var root = config.Source is { ValueKind: JsonValueKind.Object } source
            ? JsonObject.Create(source) ?? []
            : [];
        IReadOnlyList<ConfigEntry>[] entries = [config.Cams, config.VideoMixers, config.Interfaces];

        for (var index = 0; index < Sections.Length; index++)
        {
            var name = Sections[index];
            JsonElement? original = config.Source is { ValueKind: JsonValueKind.Object } file
                && file.TryGetProperty(name, out var section)
                    ? section
                    : null;

            // A section that was not an array held nothing the loader took. It stays as written
            // unless the editor has since put entries in it.
            if (original is { ValueKind: not JsonValueKind.Array } && entries[index].Count == 0)
            {
                continue;
            }

            IEnumerable<JsonElement> rejected = original is { ValueKind: JsonValueKind.Array } array
                ? array.EnumerateArray().Where(element => !ConfigLoader.IsEntry(element))
                : [];
            root[name] = new JsonArray(
            [
                .. entries[index].Select(entry => JsonNode.Parse(entry.Raw.GetRawText())),
                .. rejected.Select(element => JsonNode.Parse(element.GetRawText())),
            ]);
        }

        return root.ToJsonString(ConfigJson.Options) + "\n";
    }

    public static void WriteFile(string path, RootConfig config) => File.WriteAllText(path, Write(config));
}
