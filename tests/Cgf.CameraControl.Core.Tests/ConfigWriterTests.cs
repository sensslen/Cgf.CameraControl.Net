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

    public class WhatTheLoaderSkipped : ConfigWriterTests
    {
        private const string Rejected =
            """
            {
              "logLevel": "debug",
              "cams": [ { "type": "viscaoverip", "ip": "10.0.0.2" }, { "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1" } ],
              "videoMixers": { "type": "passthrough/default" },
              "interfaces": []
            }
            """;

        [Fact]
        public void AnEntryTheLoaderRejectedSurvivesASave()
        {
            var config = ConfigLoader.Load(Rejected, out _);

            var written = JsonNode.Parse(ConfigWriter.Write(config));

            Assert.NotNull(written);
            var cams = Assert.IsType<JsonArray>(written["cams"]);
            Assert.Equal(2, cams.Count);
            Assert.Equal("10.0.0.2", cams[1]?["ip"]?.GetValue<string>());
        }

        [Fact]
        public void AnUnknownTopLevelKeySurvivesASave()
        {
            var config = ConfigLoader.Load(Rejected, out _);

            var written = JsonNode.Parse(ConfigWriter.Write(config));

            Assert.Equal("debug", written?["logLevel"]?.GetValue<string>());
        }

        [Fact]
        public void ASectionThatWasNotAnArraySurvivesASave()
        {
            var config = ConfigLoader.Load(Rejected, out _);

            var written = JsonNode.Parse(ConfigWriter.Write(config));

            Assert.IsType<JsonObject>(written?["videoMixers"]);
        }

        [Fact]
        public void ARejectedEntryIsNotWrittenTwice()
        {
            var config = ConfigLoader.Load(Rejected, out _);

            var twice = ConfigLoader.Load(ConfigWriter.Write(ConfigLoader.Load(ConfigWriter.Write(config), out _)), out _);

            Assert.Single(twice.Cams);
            Assert.Equal(2, Assert.IsType<JsonArray>(JsonNode.Parse(ConfigWriter.Write(twice))?["cams"]).Count);
        }
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
