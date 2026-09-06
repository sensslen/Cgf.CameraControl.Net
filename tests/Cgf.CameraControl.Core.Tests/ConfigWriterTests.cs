using System.Text.Json;
using System.Text.Json.Nodes;
using Cgf.CameraControl.Core.Configuration;

namespace Cgf.CameraControl.Core.Tests;

public class ConfigWriterTests
{
    private const string Desk =
        """
        {
          "cams": [ { "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1", "tallyMode": "avonic" } ],
          "videoMixers": [ { "type": "passthrough/default", "instance": 1 } ],
          "interfaces": [ { "type": "gamepad", "instance": 1, "videoMixer": 1, "cameraMap": { "1": 1 } } ]
        }
        """;

    /// The point of writing entries from the JSON they carry: what nothing edited comes back the same,
    /// down to a property no builder in this solution has ever read.
    [Fact]
    public void WhatWasNotEditedComesBackUnchanged()
    {
        var config = ConfigLoader.Load(Desk, out _);

        var written = ConfigLoader.Load(ConfigWriter.Write(config), out var issues);

        Assert.Empty(issues);
        Assert.Equal(Raw(config.Cams[0]), Raw(written.Cams[0]));
        Assert.Equal(Raw(config.VideoMixers[0]), Raw(written.VideoMixers[0]));
        Assert.Equal(Raw(config.Interfaces[0]), Raw(written.Interfaces[0]));
    }

    [Fact]
    public void AnEditedEntryIsWrittenInPlaceOfTheOriginal()
    {
        var config = ConfigLoader.Load(Desk, out _);
        var edited = config with
        {
            Cams = [Entry("""{ "type": "viscaoverip", "instance": 1, "ip": "10.0.0.9" }""")],
        };

        var written = ConfigLoader.Load(ConfigWriter.Write(edited), out _);

        Assert.Equal("10.0.0.9", written.Cams[0].Raw.GetProperty("ip").GetString());
        Assert.Equal(Raw(config.Interfaces[0]), Raw(written.Interfaces[0]));
    }

    /// A file with nothing in it is what the editor starts from when the application is launched
    /// without a configuration, so it has to be a file the loader accepts rather than an error.
    [Fact]
    public void AnEmptyConfigurationWritesThreeEmptySections()
    {
        var written = ConfigLoader.Load(ConfigWriter.Write(RootConfig.Empty), out var issues);

        Assert.Empty(issues);
        Assert.Empty(written.Cams);
        Assert.Empty(written.VideoMixers);
        Assert.Empty(written.Interfaces);
    }

    /// Compared as content rather than as text: the writer indents what it writes, so the entry it
    /// left alone comes back saying the same thing on differently shaped lines.
    private static string Raw(ConfigEntry entry) => JsonNode.Parse(entry.Raw.GetRawText())!.ToJsonString();

    private static ConfigEntry Entry(string json)
    {
        var element = JsonDocument.Parse(json).RootElement.Clone();
        return new ConfigEntry(
            element.GetProperty("instance").GetInt32(),
            element.GetProperty("type").GetString()!,
            element);
    }
}
