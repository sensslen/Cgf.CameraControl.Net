using Cgf.CameraControl.App.Configuration;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Scenes;

namespace Cgf.CameraControl.App.Tests.Configuration;

public class SceneConfigurationTests
{
    [Fact]
    public void ReadsTheStoredPartsOfEachCamera()
    {
        var config = ConfigLoader.Load(
            """
            {
              "scenes": [
                { "name": "sermon", "cameras": { "1": { "pan": -10, "tilt": 300, "zoom": 4096 }, "2": { "focus": 12 } } }
              ]
            }
            """,
            out var issues);

        Assert.Empty(issues);
        var scene = Assert.Single(config.Scenes);
        Assert.Equal("sermon", scene.Name);
        Assert.Equal(new CameraPosition(new PanTiltPosition(-10, 300), 4096, null), scene.Cameras[1].Position);
        Assert.Equal(new CameraPosition(null, null, 12), scene.Cameras[2].Position);
    }

    [Fact]
    public void WritesScenesBackAsTheyWereRead()
    {
        var config = ConfigLoader.Load(
            """{ "scenes": [ { "name": "wide", "cameras": { "3": { "pan": 1, "tilt": 2 } } } ] }""",
            out _);

        var written = ConfigLoader.Load(ConfigWriter.Write(config), out var issues);

        Assert.Empty(issues);
        var scene = Assert.Single(written.Scenes);
        Assert.Equal("wide", scene.Name);
        Assert.Equal(new ScenePosition(1, 2, null, null), scene.Cameras[3]);
    }

    [Fact]
    public void WritesTheCamerasInInstanceOrderAndLeavesUnstoredPartsOut()
    {
        var config = RootConfig.Empty with
        {
            Scenes =
            [
                new CameraScene(
                    "wide",
                    new Dictionary<int, ScenePosition> { [2] = new(null, null, 7, null), [1] = new(1, 2, null, null) }),
            ],
        };

        Assert.Contains(
            """
                  "cameras": {
                    "1": {
                      "pan": 1,
                      "tilt": 2
                    },
                    "2": {
                      "zoom": 7
                    }
                  }
            """,
            ConfigWriter.Write(config),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AFileWithoutScenesIsWrittenWithoutTheKey()
    {
        var written = ConfigWriter.Write(ConfigLoader.Load("""{ "cams": [] }""", out _));

        Assert.DoesNotContain("scenes", written, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNameIsReadWithoutSurroundingSpaces()
    {
        var config = ConfigLoader.Load("""{ "scenes": [ { "name": " wide ", "cameras": { "1": { "zoom": 1 } } } ] }""", out _);

        Assert.Equal("wide", Assert.Single(config.Scenes).Name);
    }

    [Theory]
    [InlineData("""{ "name": "a", "cameras": { "1": { "pan": 1 } } }""", "scenes[0].cameras.1: 'pan' and 'tilt' are stored together")]
    [InlineData("""{ "name": "a", "cameras": { "1": {} } }""", "scenes[0].cameras.1: stores nothing")]
    [InlineData("""{ "name": "a", "cameras": { "1": null } }""", "scenes[0].cameras.1: expected an object, found null")]
    [InlineData("""{ "name": "a", "cameras": { "0": { "zoom": 1 } } }""", "scenes[0].cameras.0: expected a positive camera instance")]
    [InlineData("""{ "name": "a", "cameras": { "x": { "zoom": 1 } } }""", "scenes[0].cameras.x: ")]
    [InlineData("""{ "name": "a", "cameras": { "1": { "iris": 1 } } }""", "scenes[0].cameras.1.iris: The JSON property 'iris'")]
    [InlineData("""{ "name": " ", "cameras": {} }""", "scenes[0].name: expected a non-empty name")]
    [InlineData("""{ "cameras": {} }""", "scenes[0]: JSON deserialization for type 'Cgf.CameraControl.Core.Scenes.CameraScene' was missing required properties including: 'name'")]
    [InlineData("""{ "name": "a" }""", "including: 'cameras'")]
    [InlineData("""{ "name": "a", "cameras": [] }""", "scenes[0].cameras: ")]
    [InlineData("""{ "name": "a", "cameras": {}, "notes": "x" }""", "scenes[0].notes: The JSON property 'notes'")]
    [InlineData("""{ "name": "a", "cameras": { "1": { "zoom": "4096" } } }""", "scenes[0].cameras.1.zoom: ")]
    [InlineData("""{ "name": "a", "cameras": { "1": { "pan": 1.5, "tilt": 0 } } }""", "scenes[0].cameras.1.pan: ")]
    [InlineData("""{ "name": "a", "cameras": { "1": { "zoom": 65536 } } }""", "scenes[0].cameras.1.zoom: expected a number in 0 .. 65535, found 65536")]
    [InlineData("""{ "name": "a", "cameras": { "1": { "pan": -32769, "tilt": 0 } } }""", "scenes[0].cameras.1.pan: expected a number in -32768 .. 32767, found -32769")]
    [InlineData("""{ "name": "a", "cameras": { "1": { "zoom": 1 }, "01": { "focus": 2 } } }""", "scenes[0].cameras.01: camera 1 is listed twice")]
    [InlineData("""42""", "scenes[0]: ")]
    public void AMalformedSceneIsReportedAndLeftOut(string scene, string issue)
    {
        var config = ConfigLoader.Load(
            $$"""{ "scenes": [ {{scene}}, { "name": "next", "cameras": { "1": { "zoom": 1 } } } ] }""",
            out var issues);

        Assert.Equal("next", Assert.Single(config.Scenes).Name);
        Assert.Contains(issue, Assert.Single(issues), StringComparison.Ordinal);
    }

    [Fact]
    public void AScenesSectionThatIsNotAnArrayIsReported()
    {
        var config = ConfigLoader.Load("""{ "scenes": {} }""", out var issues);

        Assert.Empty(config.Scenes);
        Assert.Equal("'scenes' must be an array, found Object", Assert.Single(issues));
    }

    [Fact]
    public void ASecondSceneWithTheSameNameIsLeftOut()
    {
        var config = ConfigLoader.Load(
            """
            { "scenes": [
              { "name": "Sermon", "cameras": { "1": { "zoom": 1 } } },
              { "name": "sermon ", "cameras": { "1": { "zoom": 2 } } }
            ] }
            """,
            out var issues);

        Assert.Equal(1, Assert.Single(config.Scenes).Cameras[1].Zoom);
        Assert.Equal("scenes[1].name: another scene is already named sermon", Assert.Single(issues));
    }

    [Fact]
    public void ASceneThatCouldNotBeReadIsNotWritten()
    {
        var config = ConfigLoader.Load(
            """
            { "scenes": [
              { "name": "wide", "cameras": { "1": { "pan": 1 } } },
              { "name": "Sermon", "cameras": { "1": { "zoom": 1 } } },
              { "name": "sermon ", "cameras": { "1": { "zoom": 2 } } }
            ] }
            """,
            out _);

        var written = ConfigLoader.Load(ConfigWriter.Write(config), out var issues);

        Assert.Equal("Sermon", Assert.Single(written.Scenes).Name);
        Assert.Empty(issues);
    }
}
