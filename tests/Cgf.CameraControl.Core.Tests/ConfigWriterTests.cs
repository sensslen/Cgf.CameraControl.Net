using Cgf.CameraControl.Core.Configuration;

namespace Cgf.CameraControl.Core.Tests;

public class ConfigWriterTests
{
    private const string Desk =
        """
        {
          "cams": [ { "type": "Lens.Camera", "instance": 1, "ip": "10.0.0.1", "showTallyLight": false } ],
          "videoMixers": [ { "type": "passthrough/default", "instance": 1 } ],
          "interfaces": [ { "type": "pad", "instance": 1, "cameraMap": { "1": 1 } } ]
        }
        """;

    /// Indented, `type` first, defaults left out, `\n` and a trailing newline, so a file written on a
    /// Mac and on Windows differs only where it was edited.
    [Fact]
    public void WritesTheFileInItsOwnShape()
    {
        var written = TestSchema.Write(TestSchema.Load(Desk, out _));

        Assert.Equal(
            """
            {
              "cams": [
                {
                  "type": "Lens.Camera",
                  "instance": 1,
                  "ip": "10.0.0.1",
                  "showTallyLight": false
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
                  "type": "pad",
                  "instance": 1,
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
    public void ATypeWrittenInAnotherCaseIsWrittenBackAsRegistered()
    {
        var written = TestSchema.Write(TestSchema.Load(
            """{ "cams": [ { "instance": 1, "ip": "10.0.0.1", "type": "LENS.camera" } ] }""", out _));

        Assert.Contains("\"type\": \"Lens.Camera\"", written, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEditedRecordIsWrittenInPlaceOfTheOriginal()
    {
        var config = TestSchema.Load(Desk, out _);
        var edited = config with { Cams = [(LensCamera)config.Cams[0] with { Ip = "10.0.0.9", Port = 1259 }] };

        var written = TestSchema.Load(TestSchema.Write(edited), out var issues);

        Assert.Empty(issues);
        var camera = Assert.IsType<LensCamera>(written.Cams[0]);
        Assert.Equal("10.0.0.9", camera.Ip);
        Assert.Equal(1259, camera.Port);
    }

    /// An entry nothing could read is written back as it arrived, so opening and saving a file never
    /// shortens it.
    [Fact]
    public void AnInvalidEntryIsWrittenBackVerbatim()
    {
        var config = TestSchema.Load(
            """
            { "cams": [
              { "type": "Cgf.PtzCamera", "instance": 1, "connectionUrl": "http://10.0.0.5:5001" },
              { "type": "Lens.Camera", "instance": 2, "ip": "10.0.0.2", "port": "52381" },
              "not an entry"
            ] }
            """,
            out _);

        var written = TestSchema.Load(TestSchema.Write(config), out var issues);

        Assert.Equal(3, issues.Count);
        Assert.Equal(
            config.Cams.Select(entry => ((IInvalidConfiguration)entry).Json),
            written.Cams.Select(entry => ((IInvalidConfiguration)entry).Json));
    }

    /// A file with nothing in it is what the editor starts from when the application is launched
    /// without a configuration, so it has to be a file the loader accepts rather than an error.
    [Fact]
    public void AnEmptyConfigurationWritesThreeEmptySections()
    {
        var written = TestSchema.Load(TestSchema.Write(RootConfig.Empty), out var issues);

        Assert.Empty(issues);
        Assert.Empty(written.Cams);
        Assert.Empty(written.VideoMixers);
        Assert.Empty(written.Interfaces);
    }
}
