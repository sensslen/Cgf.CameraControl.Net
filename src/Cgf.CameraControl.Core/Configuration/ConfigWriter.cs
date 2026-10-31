using System.Text.Json.Nodes;

namespace Cgf.CameraControl.Core.Configuration;

/// Writes a configuration back out as the three arrays it was read from.
///
/// Every entry is written from the JSON it carries rather than from a deserialized shape, so an entry
/// nothing edited comes back exactly as it went in, and one whose type nothing claims survives a round
/// trip it was never parsed for. An editor changes an entry by replacing that JSON, which is the only
/// thing this has to know about editing.
///
/// Comments do not come back. The loader parses with them skipped, so they are not in memory to write.
public static class ConfigWriter
{
    public static string Write(RootConfig config)
    {
        var root = new JsonObject
        {
            ["cams"] = Section(config.Cams),
            ["videoMixers"] = Section(config.VideoMixers),
            ["interfaces"] = Section(config.Interfaces),
        };

        return root.ToJsonString(ConfigJson.Options) + "\n";
    }

    public static void WriteFile(string path, RootConfig config) => File.WriteAllText(path, Write(config));

    private static JsonArray Section(IReadOnlyList<ConfigEntry> entries) =>
        new([.. entries.Select(entry => JsonNode.Parse(entry.Raw.GetRawText()))]);
}
