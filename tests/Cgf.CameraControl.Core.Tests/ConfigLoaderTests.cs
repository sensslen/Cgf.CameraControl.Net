using Cgf.CameraControl.Core.Configuration;

namespace Cgf.CameraControl.Core.Tests;

public class ConfigLoaderTests
{
    [Fact]
    public void ReadsEverySection()
    {
        var config = ConfigLoader.Load(
            """
            {
              "cams": [ { "type": "Websocket.PtzLanc", "instance": 1, "ip": "10.0.0.1" } ],
              "videoMixers": [ { "type": "blackmagicdesign/atem", "instance": 1, "ip": "10.0.0.2", "mixEffectBlock": 0 } ],
              "interfaces": [ { "type": "logitech/F310", "instance": 1, "videoMixer": 1 } ]
            }
            """,
            out var issues);

        Assert.Empty(issues);
        Assert.Equal("Websocket.PtzLanc", Assert.Single(config.Cams).Type);
        Assert.Equal("blackmagicdesign/atem", Assert.Single(config.VideoMixers).Type);
        Assert.Equal("logitech/F310", Assert.Single(config.Interfaces).Type);
    }

    [Fact]
    public void MissingSectionsAreEmptyRatherThanFatal()
    {
        var config = ConfigLoader.Load("""{ "cams": [] }""", out var issues);

        Assert.Empty(issues);
        Assert.Empty(config.Cams);
        Assert.Empty(config.VideoMixers);
        Assert.Empty(config.Interfaces);
    }

    [Fact]
    public void KeepsTheUnknownPropertiesTheBuilderWillNeed()
    {
        var config = ConfigLoader.Load(
            """{ "cams": [ { "type": "Websocket.PtzLanc", "instance": 4, "ip": "10.0.0.9", "showTallyLight": false } ] }""",
            out _);

        var raw = Assert.Single(config.Cams).Raw;
        Assert.Equal("10.0.0.9", raw.GetProperty("ip").GetString());
        Assert.False(raw.GetProperty("showTallyLight").GetBoolean());
    }

    [Theory]
    [InlineData("""{ "cams": [ { "type": "x" } ] }""", "instance")]
    [InlineData("""{ "cams": [ { "instance": 1 } ] }""", "type")]
    [InlineData("""{ "cams": [ 42 ] }""", "must be an object")]
    [InlineData("""{ "cams": { } }""", "must be an array")]
    public void ReportsABadEntryWithoutLosingTheRest(string json, string expected)
    {
        var config = ConfigLoader.Load(json, out var issues);

        Assert.Contains(expected, Assert.Single(issues), StringComparison.Ordinal);
        Assert.Empty(config.Cams);
    }

    [Fact]
    public void OneBadEntryDoesNotDiscardItsSiblings()
    {
        var config = ConfigLoader.Load(
            """
            {
              "cams": [
                { "type": "Websocket.PtzLanc", "instance": 1 },
                { "type": "Websocket.PtzLanc" },
                { "type": "Websocket.PtzLanc", "instance": 3 }
              ]
            }
            """,
            out var issues);

        Assert.Single(issues);
        Assert.Equal([1, 3], config.Cams.Select(c => c.Instance));
    }

    [Fact]
    public void RejectsJsonThatIsNotAnObject()
    {
        Assert.Throws<ConfigFormatException>(() => ConfigLoader.Load("[]", out _));
        Assert.Throws<ConfigFormatException>(() => ConfigLoader.Load("{ not json", out _));
    }

    // The shipped configs use the builder type strings, not the ones the README documents, and the
    // oldest of them omits connectionChange.type and specialFunction entirely. The loader keeps every
    // entry it can identify and leaves the meaning of the rest to the builder that claims the type.
    [Fact]
    public void AcceptsTheShapeOfTheOldestShippedConfig()
    {
        var config = ConfigLoader.Load(
            """
            {
              "cams": [ { "type": "Cgf.PtzCamera", "instance": 1, "connectionUrl": "http://10.0.0.5:5001", "connectionPort": "/dev/ttyACM0" } ],
              "videoMixers": [ { "type": "blackmagicdesign/atem", "instance": 1, "ip": "10.0.0.6", "mixEffectBlock": 0 } ],
              "interfaces": [
                {
                  "type": "logitech/F710",
                  "instance": 1,
                  "videoMixer": 1,
                  "connectionChange": { "default": { "up": 1, "right": 2, "down": 3, "left": 4 } },
                  "cameraMap": { "1": 1, "2": 2, "3": 3 }
                }
              ]
            }
            """,
            out var issues);

        Assert.Empty(issues);
        var hmi = Assert.Single(config.Interfaces);
        Assert.False(hmi.Raw.GetProperty("connectionChange").TryGetProperty("type", out _));
        Assert.False(hmi.Raw.TryGetProperty("specialFunction", out _));
    }
}
