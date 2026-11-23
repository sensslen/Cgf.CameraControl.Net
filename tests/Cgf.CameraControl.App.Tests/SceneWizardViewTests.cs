using System.Reactive.Subjects;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.App.Scenes;
using Cgf.CameraControl.App.Views;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Scenes;
using NSubstitute;

namespace Cgf.CameraControl.App.Tests;

[Collection("Avalonia UI")]
public class SceneWizardViewTests
{
    private static readonly CameraPosition Here = new(new PanTiltPosition(5, 6), 700, 80);

    [Fact]
    public async Task CancelLeavesWithoutStoring()
    {
        await WithSheet(async (view, wizard, _) =>
        {
            Click(view, "Cancel");

            Assert.Null(await Finished(wizard));
        });
    }

    [Fact]
    public async Task SaveFinishesWithTheScene()
    {
        await WithSheet(async (view, wizard, _) =>
        {
            wizard.Name = "wide";
            wizard.Cameras[0].Lens(1, 0);
            Click(view, "NextButton");
            Click(view, "SaveButton");

            var outcome = await Finished(wizard);
            Assert.Equal("wide", outcome!.Saved!.Name);
            Assert.Equal(new CameraPosition(null, Here.Zoom, null), outcome.Saved.Cameras[1].Position);
        });
    }

    [Fact]
    public async Task DeleteAsksFirstAndFinishesOnlyWhenConfirmed()
    {
        await WithSheet(async (view, wizard, _) =>
        {
            var answer = false;
            wizard.ConfirmDelete = _ => Task.FromResult(answer);
            Click(view, "Delete");
            Assert.False(wizard.IsFinished);

            answer = true;
            Click(view, "Delete");

            Assert.Equal(new SceneWizardOutcome(null, "sermon", Deleted: true), await Finished(wizard));
        }, editing: true);
    }

    private static Task<SceneWizardOutcome?> Finished(SceneWizardViewModel wizard) =>
        wizard.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    private static void Click(Control view, string name)
    {
        var button = view.GetVisualDescendants().OfType<Button>().Single(control => control.Name == name);
        Assert.True(button.IsEffectivelyVisible && button.IsEffectivelyEnabled, name);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task WithSheet(
        Func<SceneWizardView, SceneWizardViewModel, ICameraConnection, Task> test,
        bool editing = false)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SceneWizardViewTests).Assembly);
        await session.Dispatch(async () =>
        {
            var camera = Substitute.For<ICameraConnection>();
            var position = Substitute.For<ICameraPositionControl>();
            camera.ConnectionString.Returns("visca://10.0.0.1:52381");
            camera.WhenConnectedChanged.Returns(new BehaviorSubject<bool>(true));
            camera.PositionControl.Returns(position);
            position.ReadPositionAsync(Arg.Any<CancellationToken>()).Returns(Here);
            using var wizard = new SceneWizardViewModel(new Localizer(), editing,
                editing ? [new CameraScene("sermon", new Dictionary<int, ScenePosition> { [1] = ScenePosition.From(Here) })] : [],
                new Dictionary<int, ICameraConnection> { [1] = camera });
            await wizard.ProbeAsync();
            var view = new SceneWizardView { DataContext = wizard };
            var window = new Window { Width = 800, Height = 600, Content = view };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                await test(view, wizard, camera);
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken);
    }
}
