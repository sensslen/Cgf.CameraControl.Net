using System.Text.Json;

namespace Cgf.CameraControl.Core.Configuration;

public static class ConfigJson
{
    /// Only what JsonNode.ToJsonString honours. The writer serializes nodes, so a naming policy or a
    /// converter here would apply to nothing.
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,

        // Not the platform's, so the same desk written on a Mac and on Windows differs where it was
        // edited rather than on every line.
        NewLine = "\n",
    };
}
