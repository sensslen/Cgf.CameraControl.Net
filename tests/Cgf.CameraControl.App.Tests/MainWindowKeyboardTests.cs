using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cgf.CameraControl.App.Hosting;
using Cgf.CameraControl.App.ViewModels;
using Cgf.CameraControl.App.Views;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using NSubstitute;

namespace Cgf.CameraControl.App.Tests;

[CollectionDefinition("Keyboard UI", DisableParallelization = true)]
public class KeyboardUiCollection;

[Collection("Keyboard UI")]
public class MainWindowKeyboardTests
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());

    public class FocusRouting : MainWindowKeyboardTests
    {
        [Theory]
        [InlineData("list")]
        [InlineData("button")]
        public async Task BoundArrowReachesInterface(string focusedControl)
        {
            await WithWindowAsync((window, model, view, device, _) =>
            {
                Control input = focusedControl switch
                {
                    "list" => window.GetVisualDescendants().OfType<ListBoxItem>().First(),
                    _ => window.GetVisualDescendants().OfType<Button>().First(button =>
                        ReferenceEquals(button.Command, view.Cut?.Command)),
                };
                Assert.True(input.Focus());
                var movements = new List<StickPosition>();
                using var subscription = device.LeftStick.Subscribe(movements.Add);
                window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
                window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
                window.KeyRelease(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
                Assert.Equal([new StickPosition(1, 0), new StickPosition(0, 0)], movements);
                Assert.Same(view, model.SelectedInterface);
            });
        }

        public class HeldKeys : MainWindowKeyboardTests
        {
            [Fact]
            public async Task ClosingTheActiveWindowStopsMovementWithoutWaitingForKeyUp()
            {
                await WithWindowAsync((window, _, _, device, _) =>
                {
                    var movements = new List<StickPosition>();
                    using var subscription = device.LeftStick.Subscribe(movements.Add);
                    window.KeyPress(Key.W, RawInputModifiers.None, PhysicalKey.W, "w");
                    Assert.True(window.IsActive);
                    window.Close();
                    Assert.Equal([new StickPosition(0, 1), new StickPosition(0, 0)], movements);
                });
            }
        }

        [Fact]
        public async Task UnboundArrowStillNavigatesAList()
        {
            await WithWindowAsync((window, _, _, device, _) =>
            {
                var list = new ListBox { ItemsSource = new[] { "first", "second" }, SelectedIndex = 0 };
                window.Content = list;
                Dispatcher.UIThread.RunJobs();
                Assert.True(list.GetVisualDescendants().OfType<ListBoxItem>().First().Focus());
                var movements = new List<StickPosition>();
                using var subscription = device.LeftStick.Subscribe(movements.Add);
                window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
                window.KeyRelease(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
                Assert.Equal(1, list.SelectedIndex);
                Assert.Empty(movements);
            });
        }

        [Fact]
        public async Task MenuNavigationDoesNotDriveTheCamera()
        {
            Assert.SkipWhen(OperatingSystem.IsMacOS(), "macOS hides the in-window menu in favour of the system menu bar.");
            await WithWindowAsync((window, _, _, device, _) =>
            {
                var menu = Assert.Single(window.GetVisualDescendants().OfType<Menu>());
                var items = menu.Items.OfType<MenuItem>().ToArray();
                var item = items[0];
                Assert.True(item.Focus());
                var movements = new List<StickPosition>();
                using var subscription = device.LeftStick.Subscribe(movements.Add);
                window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
                window.KeyRelease(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
                Assert.Same(items[1], window.FocusManager?.GetFocusedElement());
                Assert.Empty(movements);
            });
        }

        [Theory]
        [InlineData(Key.Enter)]
        [InlineData(Key.Space)]
        public async Task TransitionKeyRunsItsBindingInsteadOfFocusedButton(Key key)
        {
            await WithWindowAsync((window, _, view, _, mixer) =>
            {
                var other = key == Key.Enter ? view.Auto : view.Cut;
                Assert.NotNull(other);
                var button = window.GetVisualDescendants().OfType<Button>()
                    .Single(control => ReferenceEquals(control.Command, other.Command));
                Assert.True(button.Focus());
                var physicalKey = key == Key.Enter ? PhysicalKey.Enter : PhysicalKey.Space;
                window.KeyPress(key, RawInputModifiers.None, physicalKey, null);
                window.KeyPress(key, RawInputModifiers.None, physicalKey, null);
                window.KeyRelease(key, RawInputModifiers.None, physicalKey, null);
                if (key == Key.Enter)
                {
                    mixer.Received(1).Cut();
                    mixer.DidNotReceive().Auto();
                }
                else
                {
                    mixer.Received(1).Auto();
                    mixer.DidNotReceive().Cut();
                }
            });
        }

        [Fact]
        public async Task SwitchingAwayStopsMovementAndReturnsKeysToFocusedControls()
        {
            await WithWindowAsync((window, model, _, device, _) =>
            {
                var input = new TextBox();
                window.Content = input;
                Assert.True(input.Focus());
                var movements = new List<StickPosition>();
                using var subscription = device.LeftStick.Subscribe(movements.Add);
                window.KeyPress(Key.W, RawInputModifiers.None, PhysicalKey.W, "w");
                model.SelectedInterface = null;
                window.KeyRelease(Key.W, RawInputModifiers.None, PhysicalKey.W, "w");
                Key? receivedKey = null;
                input.KeyDown += (_, e) => receivedKey = e.Key;
                window.KeyPress(Key.W, RawInputModifiers.None, PhysicalKey.W, "w");
                Assert.Equal(Key.W, receivedKey);
                window.KeyRelease(Key.W, RawInputModifiers.None, PhysicalKey.W, "w");
                window.KeyTextInput("w");
                Assert.Equal([new StickPosition(0, 1), new StickPosition(0, 0)], movements);
                Assert.Equal("w", input.Text);
            });
        }
    }

    private static async Task WithWindowAsync(
        Action<MainWindow, MainViewModel, InterfaceViewModel, ControlSurfaceDevice, IVideoMixer> check)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(MainWindowKeyboardTests).Assembly);
        await session.Dispatch(async () =>
        {
            await using var host = new AppHost();
            using var model = new MainViewModel(host);
            await using var device = new ControlSurfaceDevice(1);
            var mixer = Substitute.For<IVideoMixer>();
            mixer.WhenPreviewChanged.Returns(Observable.Never<PreviewChange>());
            mixer.WhenProgramChanged.Returns(Observable.Never<int>());
            mixer.WhenConnectedChanged.Returns(Observable.Return(true));
            var config = Assert.Single(ConfigLoader.Load("""
                {
                  "interfaces": [{
                    "type": "keyboard", "instance": 1, "videoMixer": 1,
                    "connectionChange": { "type": "direct", "default": {} },
                    "cameraMap": {},
                    "keys": {
                      "pan": { "left": "Left", "right": "Right" },
                      "tilt": { "up": "W", "down": "S" },
                      "cut": "Enter", "auto": "Space"
                    }
                  }]
                }
                """, out _).Interfaces).Deserialize(InterfaceConfigurationContext.Default.KeyboardConfiguration);
            await using var hmi = new Gamepad(config, device, mixer, _ => null, Substitute.For<ILogger>());
            using var view = new InterfaceViewModel(1, hmi, new ControlSurfaceViewModel(device), [], []);
            model.Interfaces.Add(view);
            model.SelectedInterface = view;
            var window = new MainWindow { DataContext = model };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                check(window, model, view, device, mixer);
            }
            finally
            {
                window.Close();
            }

            return 0;
        }, TestContext.Current.CancellationToken);
    }
}
