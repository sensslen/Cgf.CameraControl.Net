using Cgf.CameraControl.App.Hosting;

namespace Cgf.CameraControl.App.Tests;

public class UiLoggerTests
{
    private readonly UiLogger _logger = new();

    /// The source used to be recovered by looking for a colon, which meant a message containing one
    /// was cut in half. Now it is carried, so a message may say whatever it likes.
    [Fact]
    public void TheSourceAndTheMessageAreCarriedThrough()
    {
        var entry = Capture(() => _logger.Log("Gamepad", "Selected input:2"));

        Assert.Equal("Gamepad", entry.Source);
        Assert.Equal("Selected input:2", entry.Message);
    }

    // Filtering by component must not split two cameras into two sources, but a line still has to
    // say which of them it came from.
    [Fact]
    public void TwoInstancesOfOneComponentShareASourceAndStayDistinguishable()
    {
        var first = Capture(() => _logger.Log("WebsocketCamera", "(ws://10.0.0.11/ws) connection lost"));
        var second = Capture(() => _logger.Log("WebsocketCamera", "(ws://10.0.0.12/ws) connection lost"));

        Assert.Equal(first.Source, second.Source);
        Assert.NotEqual(first.Message, second.Message);
    }

    [Fact]
    public void ErrorsAreMarkedAsSuch()
    {
        Assert.True(Capture(() => _logger.Error("Gamepad", "failed")).IsError);
        Assert.False(Capture(() => _logger.Log("Gamepad", "fine")).IsError);
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
