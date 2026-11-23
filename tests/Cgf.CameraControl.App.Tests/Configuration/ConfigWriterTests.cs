using Cgf.CameraControl.App.Configuration;
using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;
using Cgf.CameraControl.Core.Configuration;

namespace Cgf.CameraControl.App.Tests.Configuration;

public class ConfigWriterTests
{
    private const string Desk =
        """
        {
          "cams": [ { "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1", "panTiltInvert": false } ],
          "videoMixers": [ { "type": "passthrough/default", "instance": 1 } ],
          "interfaces": [ { "type": "gamepad", "instance": 1, "videoMixer": 1, "cameraMap": { "1": 1 },
                            "connectionChange": { "type": "direct", "default": {} } } ]
        }
        """;

    /// Indented, `type` first, unset settings left out, `\n` and a trailing newline, so a file
    /// written on a Mac and on Windows differs only where it was edited.
    [Fact]
    public void WritesTheFileInItsOwnShape()
    {
        var written = ConfigWriter.Write(ConfigLoader.Load(Desk, out _));

        Assert.Equal(
            """
            {
              "cams": [
                {
                  "type": "viscaoverip",
                  "instance": 1,
                  "ip": "10.0.0.1",
                  "panTiltInvert": false
                }
              ],
              "videoMixers": [
                {
                  "type": "passthrough/default",
                  "instance": 1
                }
              ],
              "interfaces": [
                {
                  "type": "gamepad",
                  "instance": 1,
                  "videoMixer": 1,
                  "connectionChange": {
                    "type": "direct",
                    "default": {}
                  },
                  "cameraMap": {
                    "1": 1
                  }
                }
              ]
            }

            """.ReplaceLineEndings("\n"),
            written);
    }

    [Fact]
    public void ANewRecordWritesOnlyWhatIsRequired()
    {
        var written = ConfigWriter.Write(new RootConfig([new ViscaOverIpCameraConfiguration { Instance = 2, Ip = "10.0.0.2" }], [], []));

        Assert.Contains(
            """
                {
                  "type": "viscaoverip",
                  "instance": 2,
                  "ip": "10.0.0.2"
                }
            """.ReplaceLineEndings("\n"),
            written,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AnEditedRecordIsWrittenInPlaceOfTheOriginal()
    {
        var config = ConfigLoader.Load(Desk, out _);
        var camera = Assert.IsType<ViscaOverIpCameraConfiguration>(config.Cams[0]);
        var edited = config with { Cams = [camera with { Ip = "10.0.0.9", Port = 1259 }] };

        var written = ConfigLoader.Load(ConfigWriter.Write(edited), out var issues);

        Assert.Empty(issues);
        var reread = Assert.IsType<ViscaOverIpCameraConfiguration>(written.Cams[0]);
        Assert.Equal("10.0.0.9", reread.Ip);
        Assert.Equal(1259, reread.Port);
    }

    /// An entry nothing could read is written back as it arrived, so opening and saving a file never
    /// shortens it.
    [Fact]
    public void AnInvalidEntryIsWrittenBackVerbatim()
    {
        var config = ConfigLoader.Load(
            """
            { "cams": [
              { "type": "Cgf.PtzCamera", "instance": 1, "connectionUrl": "http://10.0.0.5:5001" },
              { "type": "viscaoverip", "instance": 2, "ip": "10.0.0.2", "port": "52381" },
              "not an entry"
            ] }
            """,
            out _);

        var written = ConfigLoader.Load(ConfigWriter.Write(config), out var issues);

        Assert.Equal(3, issues.Count);
        Assert.Equal(
            config.Cams.Select(entry => Assert.IsType<InvalidCameraConfiguration>(entry).Json),
            written.Cams.Select(entry => Assert.IsType<InvalidCameraConfiguration>(entry).Json));
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
}
