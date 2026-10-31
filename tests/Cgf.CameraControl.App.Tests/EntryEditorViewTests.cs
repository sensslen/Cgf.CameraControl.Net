using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cgf.CameraControl.App.Editing;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.App.Views;

namespace Cgf.CameraControl.App.Tests;

[CollectionDefinition("Avalonia UI", DisableParallelization = true)]
public class AvaloniaUiCollection;

public class EntryEditorViewTests
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());

    [Collection("Avalonia UI")]
    public class CameraSpeed
    {
        [Theory]
        [InlineData(false, "viscaoverip")]
        [InlineData(true, "viscaoverip")]
        [InlineData(false, "Websocket.PtzLanc")]
        [InlineData(true, "Websocket.PtzLanc")]
        [InlineData(false, "Signalr.PtzLanc")]
        [InlineData(true, "Signalr.PtzLanc")]
        public async Task CompactInputsKeepSliderTypingAndValidation(bool dark, string type)
        {
            await using var session = HeadlessUnitTestSession.StartNew(typeof(EntryEditorViewTests));
            await session.Dispatch(() =>
            {
                var entry = new NewEntry(EntryKind.Camera, 1) { SelectedType = type }.Entry;
                var field = Assert.Single(entry.Fields.OfType<PercentField>());
                var editor = new EntryEditorView { DataContext = entry };
                var window = new Window
                {
                    Width = 560,
                    Height = 500,
                    Content = editor,
                    RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
                };
                window.Show();
                try
                {
                    Dispatcher.UIThread.RunJobs();
                    var box = Assert.Single(editor.GetVisualDescendants().OfType<TextBox>(),
                        control => control.DataContext == field);
                    var slider = Assert.Single(editor.GetVisualDescendants().OfType<Slider>());
                    Assert.InRange(box.Bounds.Height, 32, 36);
                    Assert.InRange(slider.Bounds.Height, 32, 36);
                    Assert.InRange(Math.Abs(box.Bounds.Height - slider.Bounds.Height), 0, 1);
                    Assert.Equal(Localizer.Current.Text(field.Label), AutomationProperties.GetName(box));

                    slider.Value = 50;
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal("50", box.Text);
                    box.Focus();
                    box.SelectAll();
                    window.KeyTextInput("25");
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal(25, slider.Value);
                    Assert.Equal(25, entry.ToEntry().Raw.GetProperty(field.Key).GetInt32());

                    box.SelectAll();
                    window.KeyTextInput("101");
                    Dispatcher.UIThread.RunJobs();
                    var error = Assert.Single(editor.GetVisualDescendants().OfType<TextBlock>(),
                        control => control.DataContext == field && control.Classes.Contains("wrong"));
                    Assert.NotNull(field.Error);
                    Assert.True(error.IsVisible);
                    Assert.Equal(field.Error, error.Text);
                    Assert.IsAssignableFrom<IBrush>(error.Foreground);
                    box.SelectAll();
                    window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
                    window.KeyRelease(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal(string.Empty, field.Value);
                    Assert.NotNull(field.Error);
                }
                finally
                {
                    window.Close();
                }
            }, TestContext.Current.CancellationToken);
        }
    }

    [Collection("Avalonia UI")]
    public class AddingAnEntry
    {
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task MinimumDialogSizeKeepsInputsAndFooterReachable(bool rtl, bool dark)
        {
            await using var session = HeadlessUnitTestSession.StartNew(typeof(EntryEditorViewTests));
            await session.Dispatch(() =>
            {
                var window = new AddEntryWindow
                {
                    DataContext = new NewEntry(EntryKind.Camera, 1),
                    Width = 540,
                    Height = 400,
                    FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                    RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
                };
                try
                {
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    var editor = Assert.Single(window.GetVisualDescendants().OfType<EntryEditorView>());
                    foreach (var input in editor.GetVisualDescendants().OfType<TextBox>()
                                 .Where(control => control.IsEffectivelyVisible))
                    {
                        var parent = Assert.IsAssignableFrom<Control>(input.Parent);
                        Assert.InRange(input.Bounds.Left, 0, parent.Bounds.Width);
                        Assert.InRange(input.Bounds.Right, 0, parent.Bounds.Width);
                        Assert.InRange(input.Bounds.Height, 32, 36);
                    }

                    var scroll = Assert.Single(editor.GetVisualDescendants().OfType<ScrollViewer>(),
                        control => control.Content is ItemsControl);
                    scroll.ScrollToEnd();
                    Dispatcher.UIThread.RunJobs();
                    var tally = Assert.Single(editor.GetVisualDescendants().OfType<ComboBox>());
                    var position = Assert.IsType<Point>(tally.TranslatePoint(default, scroll));
                    Assert.InRange(position.Y, 0, scroll.Bounds.Height - tally.Bounds.Height);
                    var accept = window.FindControl<Button>("Accept");
                    Assert.NotNull(accept);
                    Assert.False(accept.IsEnabled);
                    var address = Assert.Single(editor.GetVisualDescendants().OfType<TextBox>(),
                        control => control.DataContext is TextField);
                    address.Text = "10.0.0.11";
                    Dispatcher.UIThread.RunJobs();
                    Assert.True(accept.IsEnabled);
                }
                finally
                {
                    window.Close();
                }
            }, TestContext.Current.CancellationToken);
        }
    }

    [Collection("Avalonia UI")]
    public class SuggestionsAndMaps
    {
        [Fact]
        public async Task PickerAcceptsUnlistedSerialAndMapButtonsStillEditRows()
        {
            await using var session = HeadlessUnitTestSession.StartNew(typeof(EntryEditorViewTests));
            await session.Dispatch(() =>
            {
                var entry = new NewEntry(EntryKind.Interface, 1, ["known-pad"]).Entry;
                entry.Problem = "Camera 99 is not configured.";
                var serial = Assert.Single(entry.Fields.OfType<TextField>());
                var map = Assert.Single(entry.Fields.OfType<MapField>());
                var editor = new EntryEditorView { DataContext = entry };
                var window = new Window { Width = 560, Height = 800, Content = editor };
                window.Show();
                try
                {
                    Dispatcher.UIThread.RunJobs();
                    var summary = Assert.Single(editor.GetVisualDescendants().OfType<TextBlock>(),
                        control => control.Text == entry.Problem);
                    Assert.True(summary.IsVisible);
                    Assert.DoesNotContain(summary.GetVisualAncestors(), control => control is ScrollViewer);
                    var picker = Assert.Single(editor.GetVisualDescendants().OfType<AutoCompleteBox>());
                    Assert.True(picker.IsVisible);
                    Assert.InRange(picker.Bounds.Width, 220, 360);
                    picker.Text = "offline-pad";
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal("offline-pad", serial.Value);
                    Assert.Equal("offline-pad", entry.ToEntry().Raw.GetProperty("serialNumber").GetString());

                    var add = Assert.Single(editor.GetVisualDescendants().OfType<Button>(),
                        button => button.Command == map.AddRowCommand);
                    Assert.NotNull(add.Command);
                    add.Command.Execute(add.CommandParameter);
                    Dispatcher.UIThread.RunJobs();
                    Assert.Single(map.Rows);
                    var remove = Assert.Single(editor.GetVisualDescendants().OfType<Button>(),
                        button => button.Command == map.RemoveRowCommand);
                    Assert.Equal(Localizer.Current.Text("edit.delete"), AutomationProperties.GetName(remove));
                    Assert.NotNull(remove.Command);
                    remove.Command.Execute(remove.CommandParameter);
                    Dispatcher.UIThread.RunJobs();
                    Assert.Empty(map.Rows);
                }
                finally
                {
                    window.Close();
                }
            }, TestContext.Current.CancellationToken);
        }
    }

    [Collection("Avalonia UI")]
    public class BackdropFallback
    {
        [Fact]
        public async Task AnOpaqueWindowFollowsThemeChangesWithoutACompositor()
        {
            await using var session = HeadlessUnitTestSession.StartNew(typeof(EntryEditorViewTests));
            await session.Dispatch(() =>
            {
                var window = new MainWindow { RequestedThemeVariant = ThemeVariant.Light };
                window.Show();
                try
                {
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal(WindowTransparencyLevel.None, window.ActualTransparencyLevel);
                    var light = Assert.IsAssignableFrom<ISolidColorBrush>(window.Background);
                    window.RequestedThemeVariant = ThemeVariant.Dark;
                    Dispatcher.UIThread.RunJobs();
                    var dark = Assert.IsAssignableFrom<ISolidColorBrush>(window.Background);
                    Assert.NotEqual(light.Color, dark.Color);
                    Assert.Equal(255, dark.Color.A);
                    Assert.Equal(0, window.MinWidth);
                }
                finally
                {
                    window.Close();
                }
            }, TestContext.Current.CancellationToken);
        }
    }
}
