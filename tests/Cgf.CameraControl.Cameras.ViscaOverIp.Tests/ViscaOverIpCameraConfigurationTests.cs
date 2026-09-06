using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;
using Cgf.CameraControl.Core.Configuration;

namespace Cgf.CameraControl.Cameras.ViscaOverIp.Tests;

public class ViscaOverIpCameraConfigurationTests
{
    [Fact]
    public void ReadsTheShippedShape()
    {
        var config = Read(
            """{ "type": "viscaoverip", "instance": 3, "ip": "192.168.1.102", "port": 1259, "panTiltInvert": true }""");

        Assert.Equal("192.168.1.102", config.Ip);
        Assert.Equal(1259, config.Port);
        Assert.True(config.PanTiltInvert);
    }

    [Fact]
    public void PortDefaultsToTheViscaOverIpPort()
    {
        Assert.Equal(52381, Read("""{ "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1" }""").Port);
    }

    [Fact]
    public void RequiresIp()
    {
        Assert.Throws<ConfigValidationException>(() => Read("""{ "type": "viscaoverip", "instance": 1 }"""));
    }

    [Fact]
    public void RejectsAPortNoSocketCanBeOn()
    {
        Assert.Throws<ConfigValidationException>(() =>
            Read("""{ "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1", "port": 70000 }"""));
    }

    // The names are the ones the TypeScript schema accepts, less 'sony-lumens', whose two lamps sit
    // on two commands and so cannot be one payload.
    [Theory]
    [InlineData("avonic", ViscaTallyMode.Avonic)]
    [InlineData("ptzoptics", ViscaTallyMode.PtzOptics)]
    [InlineData("none", ViscaTallyMode.None)]
    public void TheTallyVendorIsNamedAsTheConfigurationFilesSpellIt(string name, ViscaTallyMode expected)
    {
        var config = Read($$"""{ "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1", "tallyMode": "{{name}}" }""");

        Assert.Equal(expected, config.TallyMode);
    }

    // Sending one vendor's payload to another vendor's camera does something unrelated, so an
    // unrecognised name has to be reported rather than guessed at.
    [Fact]
    public void RejectsAVendorItHasNoPayloadsFor()
    {
        Assert.Throws<ConfigValidationException>(() =>
            Read("""{ "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1", "tallyMode": "panasonic" }"""));
    }

    [Fact]
    public void ACameraWithNoNamedVendorDrivesNoTally()
    {
        Assert.Equal(
            ViscaTallyMode.None,
            Read("""{ "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1" }""").TallyMode);
    }

    private static ViscaOverIpCameraConfiguration Read(string json)
    {
        var config = ConfigLoader.Load($$"""{ "cams": [ {{json}} ] }""", out _);
        return Assert.Single(config.Cams)
            .Deserialize(ViscaOverIpCameraConfigurationContext.Default.ViscaOverIpCameraConfiguration);
    }
}
