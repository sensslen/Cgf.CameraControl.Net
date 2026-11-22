using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Core.VideoMixer.Passthrough;

namespace Cgf.CameraControl.Core.Tests;

public class ConfigLoaderTests
{
    [Fact]
    public void ReadsEverySectionIntoItsRecords()
    {
        var config = TestSchema.Load(
            """
            {
              "cams": [ { "type": "Lens.Camera", "instance": 1, "ip": "10.0.0.1" } ],
              "videoMixers": [ { "type": "passthrough/default", "instance": 1, "tallyMode": "program" } ],
              "interfaces": [ { "type": "pad", "instance": 1, "videoMixer": 1 } ]
            }
            """,
            out var issues);

        Assert.Empty(issues);
        Assert.Equal("10.0.0.1", Assert.IsType<LensCamera>(Assert.Single(config.Cams)).Ip);
        Assert.Equal(MixerTallyMode.Program, Assert.IsType<PassthroughConfiguration>(Assert.Single(config.VideoMixers)).TallyMode);
        Assert.Equal("pad", Assert.IsType<Pad>(Assert.Single(config.Interfaces)).Type);
    }

    [Fact]
    public void MissingSectionsAreEmptyRatherThanFatal()
    {
        var config = TestSchema.Load("""{ "cams": [] }""", out var issues);

        Assert.Empty(issues);
        Assert.Empty(config.Cams);
        Assert.Empty(config.VideoMixers);
        Assert.Empty(config.Interfaces);
    }

    [Fact]
    public void AbsentPropertiesTakeTheRecordsDefaults()
    {
        var camera = Assert.IsType<LensCamera>(Assert.Single(
            TestSchema.Load("""{ "cams": [ { "type": "Lens.Camera", "instance": 4, "ip": "10.0.0.9" } ] }""", out _).Cams));

        Assert.Equal(4, camera.Instance);
        Assert.Equal(52381, camera.Port);
        Assert.True(camera.ShowTallyLight);
    }

    /// A type string is matched without regard to case, and `type` does not have to come first.
    [Theory]
    [InlineData("""{ "type": "lens.camera", "instance": 1, "ip": "10.0.0.1" }""")]
    [InlineData("""{ "type": "LENS.CAMERA", "instance": 1, "ip": "10.0.0.1" }""")]
    [InlineData("""{ "instance": 1, "ip": "10.0.0.1", "type": "Lens.Camera" }""")]
    [InlineData("""{ "ip": "10.0.0.1", "type": "lens.Camera", "instance": 1 }""")]
    public void TheTypeIsFoundWhereverAndHoweverItIsWritten(string entry)
    {
        var camera = Assert.Single(TestSchema.Load($$"""{ "cams": [ {{entry}} ] }""", out var issues).Cams);

        Assert.Empty(issues);
        Assert.Equal("Lens.Camera", Assert.IsType<LensCamera>(camera).Type);
    }

    [Theory]
    [InlineData("""{ "cams": [ { "type": "x" } ] }""", "instance")]
    [InlineData("""{ "cams": [ { "instance": 1 } ] }""", "type")]
    [InlineData("""{ "cams": [ 42 ] }""", "must be an object")]
    [InlineData("""{ "cams": [ { "type": "Cgf.PtzCamera", "instance": 1 } ] }""", "Cgf.PtzCamera")]
    [InlineData("""{ "cams": [ { "type": "Lens.Camera", "instance": 1, "ip": "10.0.0.1", "port": 70000 } ] }""", "Lens.Camera[1].port")]
    [InlineData("""{ "cams": [ { "type": "Lens.Camera", "instance": 1, "ip": "10.0.0.1", "lens": "wide" } ] }""", "lens")]
    public void KeepsABadEntryAsInvalidAndReportsIt(string json, string expected)
    {
        var config = TestSchema.Load(json, out var issues);

        Assert.Contains(expected, Assert.Single(issues), StringComparison.Ordinal);
        var invalid = Assert.IsType<InvalidCameraConfiguration>(Assert.Single(config.Cams));
        Assert.Equal(issues[0], invalid.Problem);
        Assert.StartsWith("cams[0]", invalid.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ASectionThatIsNotAnArrayIsReported()
    {
        var config = TestSchema.Load("""{ "cams": { } }""", out var issues);

        Assert.Contains("must be an array", Assert.Single(issues), StringComparison.Ordinal);
        Assert.Empty(config.Cams);
    }

    [Fact]
    public void AnUnknownTypeNamesTheTypesThatAreKnown()
    {
        TestSchema.Load("""{ "cams": [ { "type": "Cgf.PtzCamera", "instance": 1 } ] }""", out var issues);

        Assert.Contains("Lens.Camera", Assert.Single(issues), StringComparison.Ordinal);
    }

    [Fact]
    public void OneBadEntryDoesNotDiscardItsSiblings()
    {
        var config = TestSchema.Load(
            """
            {
              "cams": [
                { "type": "Lens.Camera", "instance": 1, "ip": "10.0.0.1" },
                { "type": "Lens.Camera", "instance": 2 },
                { "type": "Lens.Camera", "instance": 3, "ip": "10.0.0.3" }
              ]
            }
            """,
            out var issues);

        Assert.StartsWith("cams[1]: Lens.Camera[2]", Assert.Single(issues), StringComparison.Ordinal);
        Assert.IsType<LensCamera>(config.Cams[0]);
        Assert.IsType<InvalidCameraConfiguration>(config.Cams[1]);
        Assert.IsType<LensCamera>(config.Cams[2]);
        Assert.Equal([1, 2, 3], config.Cams.Select(camera => camera.Instance));
    }

    [Fact]
    public void AnInvalidEntryKeepsItsJsonWithoutComments()
    {
        var invalid = Assert.IsType<InvalidCameraConfiguration>(Assert.Single(TestSchema.Load(
            """{ "cams": [ { "type": "Cgf.PtzCamera", /* old */ "instance": 7, "connectionPort": "COM6" } ] }""",
            out _).Cams));

        Assert.Equal("""{"type":"Cgf.PtzCamera","instance":7,"connectionPort":"COM6"}""", invalid.Json);
        Assert.Equal("Cgf.PtzCamera", invalid.Type);
        Assert.Equal(7, invalid.Instance);
    }

    [Fact]
    public void RejectsJsonThatIsNotAnObject()
    {
        Assert.Throws<ConfigFormatException>(() => TestSchema.Load("[]", out _));
        Assert.Throws<ConfigFormatException>(() => TestSchema.Load("{ not json", out _));
    }

    [Fact]
    public void ASchemaCannotRegisterOneTypeStringTwice()
    {
        var schema = new ConfigurationSchema(TestConfigurationContext.Default).Camera<LensCamera>("lens");

        Assert.Throws<InvalidOperationException>(() => schema.Camera<LensCamera>("LENS"));
    }
}
