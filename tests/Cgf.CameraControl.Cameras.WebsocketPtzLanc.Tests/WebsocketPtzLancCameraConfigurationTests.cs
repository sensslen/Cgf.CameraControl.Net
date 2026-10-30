using Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;
using Cgf.CameraControl.Core.Configuration;

namespace Cgf.CameraControl.Cameras.WebsocketPtzLanc.Tests;

public class WebsocketPtzLancCameraConfigurationTests
{
    [Fact]
    public void PanTiltSpeedDefaultsToFullSpeed() =>
        Assert.Equal(100, Read("""{ "type": "Websocket.PtzLanc", "instance": 1, "ip": "10.0.0.1" }""").PanTiltSpeedPercent);

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
    [InlineData("null")]
    [InlineData("\"50\"")]
    public void RejectsInvalidPanTiltSpeed(string value) =>
        Assert.Throws<ConfigValidationException>(() =>
            Read($$"""{ "type": "Websocket.PtzLanc", "instance": 1, "ip": "10.0.0.1", "panTiltSpeedPercent": {{value}} }"""));

    [Fact]
    public void ReadsTheShippedShape()
    {
        var config = Read("""{ "type": "Websocket.PtzLanc", "instance": 1, "ip": "10.0.0.124", "showTallyLight": false }""");

        Assert.Equal("10.0.0.124", config.Ip);
        Assert.False(config.ShowTallyLight);
    }

    // A tally light the operator never asked to disable has to stay on by default.
    [Fact]
    public void ShowTallyLightDefaultsOn()
    {
        Assert.True(Read("""{ "type": "Websocket.PtzLanc", "instance": 1, "ip": "10.0.0.1" }""").ShowTallyLight);
    }

    [Fact]
    public void PanTiltInvertDefaultsOff()
    {
        Assert.False(Read("""{ "type": "Websocket.PtzLanc", "instance": 1, "ip": "10.0.0.1" }""").PanTiltInvert);
    }

    [Fact]
    public void RequiresIp()
    {
        Assert.Throws<ConfigValidationException>(() => Read("""{ "type": "Websocket.PtzLanc", "instance": 1 }"""));
    }

    private static WebsocketPtzLancCameraConfiguration Read(string json)
    {
        var config = ConfigLoader.Load($$"""{ "cams": [ {{json}} ] }""", out _);
        return Assert.Single(config.Cams)
            .Deserialize(WebsocketPtzLancCameraConfigurationContext.Default.WebsocketPtzLancCameraConfiguration);
    }
}
