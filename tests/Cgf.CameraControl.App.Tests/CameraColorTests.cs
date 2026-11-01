using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cgf.CameraControl.App.Hosting;
using Cgf.CameraControl.App.ViewModels;
using Cgf.CameraControl.App.Views;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;
using Cgf.CameraControl.Core.Logger;
using NSubstitute;

namespace Cgf.CameraControl.App.Tests;

[Collection("Avalonia UI")]
public class CameraColorTests
{
    public class Capability : CameraColorTests
    {
        [Fact]
        public async Task UnloadedCameraDoesNotStartAQueuedColorRead()
        {
            await WithWindow((_, _, _, _) =>
            {
                var camera = Substitute.For<ICameraConnection, ICameraColorControl>();
                var color = Assert.IsAssignableFrom<ICameraColorControl>(camera);
                camera.WhenConnectedChanged.Returns(Observable.Return(true));
                color.WhenColorChanged.Returns(Observable.Return<CameraColorState?>(new(true, 128, 128)));
                var model = new CameraViewModel(new ObservedCamera(camera));
                model.Dispose();
                Dispatcher.UIThread.RunJobs();
                color.DidNotReceive().RefreshColor();
                Assert.False(model.SupportsColorCorrection);
            });
        }

        [Fact]
        public async Task WheelOnlyAppearsAfterCameraReportsSupport()
        {
            await WithWindow((window, model, color, state) =>
            {
                var button = FindWheel(window, model);
                Assert.False(button.IsVisible);
                Assert.False(model.OnePushWhiteBalanceCommand.CanExecute(null));
                color.Received(1).RefreshColor();
                state.OnNext(new(true, 120, 140));
                Dispatcher.UIThread.RunJobs();
                Assert.True(button.IsVisible);
                Assert.True(button.IsEnabled);
                Assert.True(model.OnePushWhiteBalanceCommand.CanExecute(null));
                Assert.Equal(120, model.RedGain);
                Assert.Equal(140, model.BlueGain);
                color.DidNotReceive().SetManualWhiteBalance(Arg.Any<bool>());
                color.DidNotReceive().SetRedGain(Arg.Any<int>());
                color.DidNotReceive().SetBlueGain(Arg.Any<int>());
                state.OnNext(null);
                Dispatcher.UIThread.RunJobs();
                Assert.True(button.IsVisible);
                Assert.False(model.CanCorrectColor);
            });
        }

        [Fact]
        public async Task NonViscaCameraHasNoWheel()
        {
            await WithWindow((window, _, _, _) =>
            {
                var main = Assert.IsType<MainViewModel>(window.DataContext);
                var camera = Substitute.For<ICameraConnection>();
                camera.ConnectionString.Returns("lanc://camera");
                camera.WhenConnectedChanged.Returns(Observable.Return(true));
                using var model = new CameraViewModel(new ObservedCamera(camera));
                main.Cameras.Add(model);
                Dispatcher.UIThread.RunJobs();
                Assert.False(FindWheel(window, model).IsVisible);
            });
        }
    }

    public class Layout : CameraColorTests
    {
        [Theory]
        [InlineData(FlowDirection.LeftToRight, false)]
        [InlineData(FlowDirection.LeftToRight, true)]
        [InlineData(FlowDirection.RightToLeft, false)]
        [InlineData(FlowDirection.RightToLeft, true)]
        public async Task MovementStatusStaysAlignedWhenTheWheelAppearsOrDisappears(FlowDirection direction, bool dark)
        {
            await WithWindow((window, model, _, state) =>
            {
                window.FlowDirection = direction;
                window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
                var main = Assert.IsType<MainViewModel>(window.DataContext);
                var camera = Substitute.For<ICameraConnection>();
                camera.ConnectionString.Returns("lanc://a-camera-with-a-longer-address");
                camera.WhenConnectedChanged.Returns(Observable.Return(true));
                using var unsupported = new CameraViewModel(new ObservedCamera(camera));
                main.Cameras.Add(unsupported);
                Dispatcher.UIThread.RunJobs();
                var initialX = MovementX(window, model);
                Assert.Equal(initialX, MovementX(window, unsupported), 3);
                Assert.False(FindWheel(window, model).IsVisible);
                Assert.False(FindWheel(window, unsupported).IsVisible);

                state.OnNext(new(true, 120, 140));
                Dispatcher.UIThread.RunJobs();
                Assert.True(FindWheel(window, model).IsVisible);
                Assert.Equal(initialX, MovementX(window, model), 3);
                Assert.Equal(initialX, MovementX(window, unsupported), 3);

                model.IsConnected = false;
                Dispatcher.UIThread.RunJobs();
                Assert.False(FindWheel(window, model).IsVisible);
                Assert.Equal(initialX, MovementX(window, model), 3);
                Assert.Equal(initialX, MovementX(window, unsupported), 3);
            });
        }
    }

    public class Panel : CameraColorTests
    {
        [Theory]
        [InlineData(PlacementMode.Left, false)]
        [InlineData(PlacementMode.Left, true)]
        [InlineData(PlacementMode.Right, false)]
        [InlineData(PlacementMode.Right, true)]
        public async Task PointerTracksItsCameraButtonAfterPopupPlacement(PlacementMode placement, bool dark)
        {
            await WithWindow((window, model, _, state) =>
            {
                window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
                window.Position = new PixelPoint(200, 400);
                state.OnNext(new(true, 120, 140));
                Dispatcher.UIThread.RunJobs();
                var button = FindWheel(window, model);
                var flyout = Assert.IsType<Flyout>(button.Flyout);
                flyout.Placement = placement;
                Click(window, button);
                var presenter = Assert.IsAssignableFrom<Control>(flyout.Popup.Child);
                var pointers = presenter.GetVisualDescendants().OfType<Avalonia.Controls.Panel>()
                    .Where(panel => panel.Name is "LeftPointer" or "RightPointer").ToArray();
                Assert.Equal(2, pointers.Length);
                var pointer = Assert.Single(pointers, panel => panel.IsVisible);
                var target = button.PointToScreen(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2));
                var origin = presenter.PointToScreen(default);
                var pointLeft = target.X < origin.X + presenter.Bounds.Width / 2;
                Assert.Equal(pointLeft ? "LeftPointer" : "RightPointer", pointer.Name);
                var tip = pointer.PointToScreen(new Point(pointLeft ? 0 : 11, 10));
                Assert.InRange(Math.Abs(tip.Y - target.Y), 0, 1);
                Assert.InRange(Math.Abs(tip.X - target.X), button.Bounds.Width / 2, button.Bounds.Width / 2 + 20);
                window.MouseDown(new Point(30, 180), MouseButton.Left);
                window.MouseUp(new Point(30, 180), MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                Assert.False(flyout.IsOpen);
            });
        }

        [Fact]
        public async Task RealViscaReadbackKeepsWheelOpenAndDoesNotSendDefaults()
        {
            var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(CameraColorTests).Assembly);
            await session.Dispatch(async () =>
            {
                await using var host = new AppHost();
                using var main = new MainViewModel(host) { AreDevicesExpanded = true };
                using var connected = new BehaviorSubject<bool>(true);
                using var replies = new Subject<byte[]>();
                var transport = Substitute.For<IViscaTransport>();
                transport.WhenConnectedChanged.Returns(connected);
                transport.Received.Returns(replies);
                await using var camera = new ViscaOverIpCamera(new() { Ip = "127.0.0.1" },
                    transport, Substitute.For<ILogger>());
                using var model = new CameraViewModel(new ObservedCamera(camera));
                main.Cameras.Add(model);
                var window = new MainWindow { DataContext = main };
                window.Show();
                try
                {
                    Dispatcher.UIThread.RunJobs();
                    ReadColor(replies, 120, 140);
                    Dispatcher.UIThread.RunJobs();
                    var button = FindWheel(window, model);
                    Click(window, button);
                    var flyout = Assert.IsType<Flyout>(button.Flyout);
                    Assert.True(flyout.IsOpen);
                    Assert.True(button.IsVisible);
                    Assert.False(model.CanCorrectColor);
                    ReadColor(replies, 100, 160);
                    Dispatcher.UIThread.RunJobs();
                    Assert.True(flyout.IsOpen);
                    Assert.True(model.CanAdjustGains);
                    Assert.Equal(100, model.RedGain);
                    Assert.Equal(160, model.BlueGain);
                    transport.Received(6).Send(Arg.Is<byte[]>(packet => packet[1] == 0x09));
                    transport.DidNotReceive().Send(Arg.Is<byte[]>(packet => packet[1] == 0x01));
                }
                finally
                {
                    window.Close();
                    Dispatcher.UIThread.RunJobs();
                }
            }, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task ClickingWheelOpensPanelAndOutsideClickClosesIt()
        {
            await WithWindow((window, model, _, state) =>
            {
                state.OnNext(new(true, 120, 140));
                Dispatcher.UIThread.RunJobs();
                var button = FindWheel(window, model);
                Click(window, button);
                var flyout = Assert.IsType<Flyout>(button.Flyout);
                Assert.True(flyout.IsOpen);
                var panel = Assert.IsType<CameraColorView>(flyout.Content);
                Assert.Same(model, panel.DataContext);
                Assert.Equal(120, panel.GetVisualDescendants().OfType<Slider>()
                    .Single(slider => slider.Name == "RedGainSlider").Value);
                Assert.Equal(140, panel.GetVisualDescendants().OfType<Slider>()
                    .Single(slider => slider.Name == "BlueGainSlider").Value);
                window.MouseDown(new Point(30, 180), MouseButton.Left);
                window.MouseUp(new Point(30, 180), MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                Assert.False(flyout.IsOpen);
                Click(window, button);
                Assert.True(flyout.IsOpen);
                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                Dispatcher.UIThread.RunJobs();
                Assert.False(flyout.IsOpen);
            });
        }

        [Fact]
        public async Task PanelControlsSendOnlyTheirCameraCommands()
        {
            await WithWindow((window, model, color, state) =>
            {
                state.OnNext(new(false, 120, 140));
                Dispatcher.UIThread.RunJobs();
                Click(window, FindWheel(window, model));
                var flyout = Assert.IsType<Flyout>(FindWheel(window, model).Flyout);
                var panel = Assert.IsType<CameraColorView>(flyout.Content);
                var controls = panel.GetVisualDescendants().OfType<Slider>().ToArray();
                Assert.All(controls, slider => Assert.False(slider.IsEffectivelyEnabled));
                panel.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = true;
                Dispatcher.UIThread.RunJobs();
                color.Received(1).SetManualWhiteBalance(true);
                Assert.All(controls, slider => Assert.True(slider.IsEffectivelyEnabled));
                controls.Single(slider => slider.Name == "RedGainSlider").Value = 110;
                controls.Single(slider => slider.Name == "BlueGainSlider").Value = 150;
                Dispatcher.UIThread.RunJobs();
                color.Received(1).SetRedGain(110);
                color.Received(1).SetBlueGain(150);
                var onePush = panel.GetVisualDescendants().OfType<Button>().Single();
                var popup = Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(onePush));
                Click(popup, onePush);
                color.Received(1).OnePushWhiteBalance();
                Assert.True(flyout.IsOpen);
            });
        }
    }

    private static Button FindWheel(MainWindow window, CameraViewModel model) =>
        window.GetVisualDescendants().OfType<Button>().Single(button =>
            button.Name == "ColorCorrectionButton" && ReferenceEquals(button.DataContext, model));

    private static double MovementX(MainWindow window, CameraViewModel model)
    {
        var row = Assert.IsType<Grid>(FindWheel(window, model).Parent);
        var status = row.Children.OfType<TextBlock>().Single(text => Grid.GetColumn(text) == 2);
        Assert.True(status.Bounds.Width > 0);
        return Assert.IsType<Point>(status.TranslatePoint(default, window)).X;
    }

    private static void ReadColor(Subject<byte[]> replies, int red, int blue)
    {
        replies.OnNext([0x90, 0x50, 0x05, 0xFF]);
        replies.OnNext([0x90, 0x50, 0, 0, (byte)(red >> 4), (byte)(red & 0x0F), 0xFF]);
        replies.OnNext([0x90, 0x50, 0, 0, (byte)(blue >> 4), (byte)(blue & 0x0F), 0xFF]);
    }

    private static void Click(TopLevel window, Control control)
    {
        var point = Assert.IsType<Point>(control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window));
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task WithWindow(
        Action<MainWindow, CameraViewModel, ICameraColorControl, BehaviorSubject<CameraColorState?>> check)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(CameraColorTests).Assembly);
        await session.Dispatch(async () =>
        {
            await using var host = new AppHost();
            using var main = new MainViewModel(host) { AreDevicesExpanded = true };
            var camera = Substitute.For<ICameraConnection, ICameraColorControl>();
            var color = Assert.IsAssignableFrom<ICameraColorControl>(camera);
            camera.ConnectionString.Returns("visca://127.0.0.1:52381");
            camera.WhenConnectedChanged.Returns(Observable.Return(true));
            using var state = new BehaviorSubject<CameraColorState?>(null);
            color.WhenColorChanged.Returns(state);
            using var model = new CameraViewModel(new ObservedCamera(camera));
            main.Cameras.Add(model);
            var window = new MainWindow { DataContext = main };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                check(window, model, color, state);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        }, TestContext.Current.CancellationToken);
    }
}
