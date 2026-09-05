using Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;
using Cgf.CameraControl.Core.Configuration;

namespace Cgf.CameraControl.Cameras.SignalrPtzLanc.Tests;

public class SignalrPtzLancCameraConfigurationTests
{
    [Fact]
    public void ReadsTheShippedShape()
    {
        var config = Read(
            """
            { "type": "Signalr.PtzLanc", "instance": 1, "connectionUrl": "http://192.168.1.101:5000",
              "connectionPort": "COM6", "panTiltInvert": true }
            """);

        Assert.Equal("http://192.168.1.101:5000", config.ConnectionUrl);
        Assert.Equal("COM6", config.ConnectionPort);
        Assert.True(config.PanTiltInvert);
    }

    [Fact]
    public void PanTiltInvertDefaultsOff()
    {
        Assert.False(Read(Minimal).PanTiltInvert);
    }

    [Fact]
    public void RequiresTheControllerUrl()
    {
        Assert.Throws<ConfigValidationException>(() =>
            Read("""{ "type": "Signalr.PtzLanc", "instance": 1, "connectionPort": "COM6" }"""));
    }

    [Fact]
    public void RequiresTheSerialPort()
    {
        Assert.Throws<ConfigValidationException>(() =>
            Read("""{ "type": "Signalr.PtzLanc", "instance": 1, "connectionUrl": "http://10.0.0.5:5000" }"""));
    }

    // The value is concatenated with a path and requested, so a host on its own never reaches
    // anything and has to be reported here rather than as a connection that never comes up.
    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("ftp://10.0.0.5")]
    [InlineData("")]
    public void RejectsSomethingThatIsNotAnHttpUrl(string url)
    {
        Assert.Throws<ConfigValidationException>(() =>
            Read($$"""{ "type": "Signalr.PtzLanc", "instance": 1, "connectionUrl": "{{url}}", "connectionPort": "COM6" }"""));
    }

    // The paths are appended to it, so one trailing slash would otherwise double up.
    [Fact]
    public void ATrailingSlashIsDropped()
    {
        var config = Read(
            """{ "type": "Signalr.PtzLanc", "instance": 1, "connectionUrl": "http://10.0.0.5:5000/", "connectionPort": "COM6" }""");

        Assert.Equal("http://10.0.0.5:5000", config.ConnectionUrl);
    }

    // Linux and macOS name the port by device path rather than COMn.
    [Fact]
    public void TheSerialPortIsTakenAsTheControllerSpellsIt()
    {
        var config = Read(
            """{ "type": "Signalr.PtzLanc", "instance": 1, "connectionUrl": "http://10.0.0.5:5000", "connectionPort": "/dev/ttyUSB0" }""");

        Assert.Equal("/dev/ttyUSB0", config.ConnectionPort);
    }

    private const string Minimal =
        """{ "type": "Signalr.PtzLanc", "instance": 1, "connectionUrl": "http://10.0.0.5:5000", "connectionPort": "COM6" }""";

    private static SignalrPtzLancCameraConfiguration Read(string json)
    {
        var config = ConfigLoader.Load($$"""{ "cams": [ {{json}} ] }""", out _);
        return Assert.Single(config.Cams)
            .Deserialize(SignalrPtzLancCameraConfigurationContext.Default.SignalrPtzLancCameraConfiguration);
    }
}
