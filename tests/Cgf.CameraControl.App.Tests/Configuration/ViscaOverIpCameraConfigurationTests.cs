using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;

namespace Cgf.CameraControl.App.Tests.Configuration;

public class ViscaOverIpCameraConfigurationTests
{
    [Fact]
    public void AnAbsentPanTiltSpeedIsLeftUnset() =>
        Assert.Null(Read("""{ "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1" }""").PanTiltSpeedPercent);

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void ReadsPanTiltSpeedPercent(int percent) =>
        Assert.Equal(percent, Read($$"""{ "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1", "panTiltSpeedPercent": {{percent}} }""").PanTiltSpeedPercent);

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("101")]
    [InlineData("50.5")]
    [InlineData("\"50\"")]
    public void RejectsInvalidPanTiltSpeed(string value) =>
        Rejected($$"""{ "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1", "panTiltSpeedPercent": {{value}} }""");

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
    public void AnAbsentPortIsLeftUnset()
    {
        Assert.Null(Read("""{ "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1" }""").Port);
    }

    [Fact]
    public void RequiresIp()
    {
        Rejected("""{ "type": "viscaoverip", "instance": 1 }""");
    }

    [Fact]
    public void RejectsAPortNoSocketCanBeOn()
    {
        Rejected("""{ "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1", "port": 70000 }""");
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
        Rejected("""{ "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1", "tallyMode": "panasonic" }""");
    }

    [Fact]
    public void AnAbsentVendorIsLeftUnset()
    {
        Assert.Null(Read("""{ "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1" }""").TallyMode);
    }

    private static ViscaOverIpCameraConfiguration Read(string json) => ConfigurationReader.Read<ViscaOverIpCameraConfiguration>("cams", json);

    private static string Rejected(string json) => ConfigurationReader.Rejected("cams", json);
}
