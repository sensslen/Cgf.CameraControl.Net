using Cgf.CameraControl.App.Configuration;
using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Core.VideoMixer.Passthrough;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.App.Tests.Configuration;

public class ConfigLoaderTests
{
    private const string Pad =
        """{ "type": "gamepad", "instance": 1, "videoMixer": 1, "cameraMap": {}, "connectionChange": { "type": "direct", "default": {} } }""";

    [Fact]
    public void ReadsEverySectionIntoItsRecords()
    {
        var config = ConfigLoader.Load(
            $$"""
            {
              "cams": [ { "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1" } ],
              "videoMixers": [ { "type": "passthrough/default", "instance": 1, "tallyMode": "program" } ],
              "interfaces": [ {{Pad}} ]
            }
            """,
            out var issues);

        Assert.Empty(issues);
        Assert.Equal("10.0.0.1", Assert.IsType<ViscaOverIpCameraConfiguration>(Assert.Single(config.Cams)).Ip);
        Assert.Equal(MixerTallyMode.Program, Assert.IsType<PassthroughConfiguration>(Assert.Single(config.VideoMixers)).TallyMode);
        Assert.Equal(GamepadConfiguration.TypeName, Assert.IsType<GamepadConfiguration>(Assert.Single(config.Interfaces)).Type);
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
    public void AbsentOptionalSettingsAreLeftUnset()
    {
        var camera = Assert.IsType<ViscaOverIpCameraConfiguration>(Assert.Single(
            ConfigLoader.Load("""{ "cams": [ { "type": "viscaoverip", "instance": 4, "ip": "10.0.0.9" } ] }""", out _).Cams));

        Assert.Equal(4, camera.Instance);
        Assert.Null(camera.Port);
        Assert.Null(camera.TallyMode);
    }

    [Fact]
    public void TheTypeMayStandAnywhereInTheEntry()
    {
        var camera = Assert.Single(
            ConfigLoader.Load("""{ "cams": [ { "instance": 1, "ip": "10.0.0.1", "type": "viscaoverip" } ] }""", out var issues).Cams);

        Assert.Empty(issues);
        Assert.IsType<ViscaOverIpCameraConfiguration>(camera);
    }

    [Theory]
    [InlineData("VISCAOVERIP")]
    [InlineData("ViscaOverIp")]
    public void TheTypeIsMatchedExactly(string type)
    {
        var config = ConfigLoader.Load(
            $$"""{ "cams": [ { "type": "{{type}}", "instance": 1, "ip": "10.0.0.1" } ] }""", out var issues);

        Assert.StartsWith($"cams[0]: {type}[1]", Assert.Single(issues), StringComparison.Ordinal);
        Assert.IsType<InvalidCameraConfiguration>(Assert.Single(config.Cams));
    }

    [Theory]
    [InlineData("""{ "cams": [ { "type": "x" } ] }""", "instance")]
    [InlineData("""{ "cams": [ { "instance": 1 } ] }""", "type")]
    [InlineData("""{ "cams": [ 42 ] }""", "must be an object")]
    [InlineData("""{ "cams": [ { "type": "Cgf.PtzCamera", "instance": 1 } ] }""", "Cgf.PtzCamera")]
    [InlineData("""{ "cams": [ { "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1", "port": 70000 } ] }""", "viscaoverip[1].port")]
    [InlineData("""{ "cams": [ { "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1", "lens": "wide" } ] }""", "lens")]
    public void KeepsABadEntryAsInvalidAndReportsIt(string json, string expected)
    {
        var config = ConfigLoader.Load(json, out var issues);

        Assert.Contains(expected, Assert.Single(issues), StringComparison.Ordinal);
        var invalid = Assert.IsType<InvalidCameraConfiguration>(Assert.Single(config.Cams));
        Assert.Equal(issues[0], invalid.Problem);
        Assert.StartsWith("cams[0]", invalid.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownTypeIsReportedAgainstItsEntry()
    {
        ConfigLoader.Load("""{ "cams": [ { "type": "Cgf.PtzCamera", "instance": 1 } ] }""", out var issues);

        Assert.StartsWith("cams[0]: Cgf.PtzCamera[1]", Assert.Single(issues), StringComparison.Ordinal);
    }

    [Fact]
    public void ASectionThatIsNotAnArrayIsReported()
    {
        var config = ConfigLoader.Load("""{ "cams": { } }""", out var issues);

        Assert.Contains("must be an array", Assert.Single(issues), StringComparison.Ordinal);
        Assert.Empty(config.Cams);
    }

    [Fact]
    public void OneBadEntryDoesNotDiscardItsSiblings()
    {
        var config = ConfigLoader.Load(
            """
            {
              "cams": [
                { "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1" },
                { "type": "viscaoverip", "instance": 2 },
                { "type": "viscaoverip", "instance": 3, "ip": "10.0.0.3" }
              ]
            }
            """,
            out var issues);

        Assert.StartsWith("cams[1]: viscaoverip[2]", Assert.Single(issues), StringComparison.Ordinal);
        Assert.IsType<ViscaOverIpCameraConfiguration>(config.Cams[0]);
        Assert.IsType<InvalidCameraConfiguration>(config.Cams[1]);
        Assert.IsType<ViscaOverIpCameraConfiguration>(config.Cams[2]);
        Assert.Equal([1, 2, 3], config.Cams.Select(camera => camera.Instance));
    }

    [Fact]
    public void AnInvalidEntryKeepsItsJsonWithoutComments()
    {
        var invalid = Assert.IsType<InvalidCameraConfiguration>(Assert.Single(ConfigLoader.Load(
            """{ "cams": [ { "type": "Cgf.PtzCamera", /* old */ "instance": 7, "connectionPort": "COM6" } ] }""",
            out _).Cams));

        Assert.Equal("""{"type":"Cgf.PtzCamera","instance":7,"connectionPort":"COM6"}""", invalid.Json);
        Assert.Equal("Cgf.PtzCamera", invalid.Type);
        Assert.Equal(7, invalid.Instance);
    }

    [Fact]
    public void CommentsAndTrailingCommasInsideAnEntryAreRead()
    {
        var config = ConfigLoader.Load(
            """{ "cams": [ { "type": "viscaoverip", /* desk left */ "instance": 1, "ip": "10.0.0.1", } ] }""", out var issues);

        Assert.Empty(issues);
        Assert.IsType<ViscaOverIpCameraConfiguration>(Assert.Single(config.Cams));
    }

    [Fact]
    public void RejectsJsonThatIsNotAnObject()
    {
        Assert.Throws<ConfigFormatException>(() => ConfigLoader.Load("[]", out _));
        Assert.Throws<ConfigFormatException>(() => ConfigLoader.Load("{ not json", out _));
    }
}
