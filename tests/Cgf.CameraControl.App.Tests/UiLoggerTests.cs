using Cgf.CameraControl.App.Hosting;

namespace Cgf.CameraControl.App.Tests;

public class UiLoggerTests
{
    private readonly UiLogger _logger = new();

    [Fact]
    public void APlainPrefixBecomesTheSource()
    {
        var entry = Capture(() => _logger.Log("Gamepad:Selected input:2"));

        Assert.Equal("Gamepad", entry.Source);
        Assert.Equal("Selected input:2", entry.Message);
    }

    [Fact]
    public void APrefixWithSpacesIsStillOneSource()
    {
        var entry = Capture(() => _logger.Log("Passthrough video mixer:running macro: 4"));

        Assert.Equal("Passthrough video mixer", entry.Source);
    }

    // The camera puts its address in its prefix, and that address contains a colon.
    [Fact]
    public void AnAddressInThePrefixDoesNotEndIt()
    {
        var entry = Capture(() => _logger.Log("WebsocketCamera(ws://10.0.0.11/ws):connection lost"));

        Assert.Equal("WebsocketCamera", entry.Source);
        Assert.Equal("(ws://10.0.0.11/ws) connection lost", entry.Message);
    }

    // Filtering by component must not split two cameras into two sources, but a line still has to
    // say which of them it came from.
    [Fact]
    public void TwoInstancesOfOneComponentShareASourceAndStayDistinguishable()
    {
        var first = Capture(() => _logger.Log("WebsocketCamera(ws://10.0.0.11/ws):connection lost"));
        var second = Capture(() => _logger.Log("WebsocketCamera(ws://10.0.0.12/ws):connection lost"));

        Assert.Equal(first.Source, second.Source);
        Assert.NotEqual(first.Message, second.Message);
    }

    [Fact]
    public void AnInstanceInBracketsIsTreatedTheSameWay()
    {
        var entry = Capture(() => _logger.Log("SDL:gamepad[1] matches no connected pad"));

        Assert.Equal("SDL", entry.Source);
        Assert.Equal("gamepad[1] matches no connected pad", entry.Message);
    }

    [Fact]
    public void AnUnprefixedLineIsAttributedToTheApplication()
    {
        var entry = Capture(() => _logger.Log("something happened"));

        Assert.Equal("Application", entry.Source);
        Assert.Equal("something happened", entry.Message);
    }

    [Fact]
    public void ErrorsAreMarkedAsSuch()
    {
        Assert.True(Capture(() => _logger.Error("Gamepad:failed")).IsError);
        Assert.False(Capture(() => _logger.Log("Gamepad:fine")).IsError);
    }

    private LogEntry Capture(Action write)
    {
        LogEntry? captured = null;
        using (_logger.WhenLogged.Subscribe(entry => captured = entry))
        {
            write();
        }

        Assert.NotNull(captured);
        return captured;
    }
}
