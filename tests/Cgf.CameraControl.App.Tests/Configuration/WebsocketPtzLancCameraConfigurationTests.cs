using Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;

namespace Cgf.CameraControl.App.Tests.Configuration;

public class WebsocketPtzLancCameraConfigurationTests
{
    [Fact]
    public void AnAbsentPanTiltSpeedIsLeftUnset() =>
        Assert.Null(Read("""{ "type": "Websocket.PtzLanc", "instance": 1, "ip": "10.0.0.1" }""").PanTiltSpeedPercent);

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void ReadsPanTiltSpeedPercent(int percent) =>
        Assert.Equal(percent, Read($$"""{ "type": "Websocket.PtzLanc", "instance": 1, "ip": "10.0.0.1", "panTiltSpeedPercent": {{percent}} }""").PanTiltSpeedPercent);

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("101")]
    [InlineData("50.5")]
    [InlineData("\"50\"")]
    public void RejectsInvalidPanTiltSpeed(string value) =>
        Rejected($$"""{ "type": "Websocket.PtzLanc", "instance": 1, "ip": "10.0.0.1", "panTiltSpeedPercent": {{value}} }""");

    [Fact]
    public void ReadsTheShippedShape()
    {
        var config = Read("""{ "type": "Websocket.PtzLanc", "instance": 1, "ip": "10.0.0.124", "showTallyLight": false }""");

        Assert.Equal("10.0.0.124", config.Ip);
        Assert.False(config.ShowTallyLight);
    }

    [Fact]
    public void AbsentSwitchesAreLeftUnset()
    {
        var config = Read("""{ "type": "Websocket.PtzLanc", "instance": 1, "ip": "10.0.0.1" }""");

        Assert.Null(config.ShowTallyLight);
        Assert.Null(config.PanTiltInvert);
    }

    [Fact]
    public void RequiresIp()
    {
        Rejected("""{ "type": "Websocket.PtzLanc", "instance": 1 }""");
    }

    private static WebsocketPtzLancCameraConfiguration Read(string json) => ConfigurationReader.Read<WebsocketPtzLancCameraConfiguration>("cams", json);

    private static string Rejected(string json) => ConfigurationReader.Rejected("cams", json);
}
