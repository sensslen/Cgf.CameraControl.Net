using Avalonia;
using Cgf.CameraControl.App.Views;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.App.Tests;

public class GamepadWireframeTests
{
    [Theory]
    [InlineData(true, 1, 0, -9, 0)]
    [InlineData(true, -1, 0, 9, 0)]
    [InlineData(true, 0, -1, 0, -9)]
    [InlineData(true, 0, 1, 0, 9)]
    [InlineData(false, 1, 0, -9, 0)]
    [InlineData(false, -1, 0, 9, 0)]
    [InlineData(false, 0, 1, 0, -9)]
    [InlineData(false, 0, -1, 0, 9)]
    [InlineData(true, 0, 0, 0, 0)]
    [InlineData(false, 0, 0, 0, 0)]
    public void CameraCoordinatesDrawThePhysicalStickDirection(
        bool left, double x, double y, double screenX, double screenY)
    {
        Assert.Equal(new Point(screenX, screenY),
            GamepadWireframe.StickOffset(new StickPosition(x, y), left));
    }
}
