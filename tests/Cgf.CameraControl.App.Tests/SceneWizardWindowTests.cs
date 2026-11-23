using System.Reactive.Subjects;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.App.Scenes;
using Cgf.CameraControl.App.Views;
using Cgf.CameraControl.Core.CameraConnection;
using NSubstitute;

namespace Cgf.CameraControl.App.Tests;

[Collection("Avalonia UI")]
public class SceneWizardWindowTests
{
    [Fact]
    public async Task EscapeClosesTheDialogWithoutStoring()
    {
        await WithDialog(async (dialog, wizard, showing) =>
        {
            dialog.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            await showing.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Null(await wizard.Completion);
        });
    }

    [Fact]
    public async Task FinishingTheWizardClosesTheDialog()
    {
        await WithDialog(async (_, wizard, showing) =>
        {
            wizard.Cancel();
            Dispatcher.UIThread.RunJobs();

            await showing.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task ClosingWhileStoringKeepsTheDialogOpen()
    {
        await WithDialog(async (dialog, wizard, showing) =>
        {
            await wizard.ProbeAsync();
            wizard.Name = "wide";
            wizard.Cameras[0].Lens(1, 0);
            wizard.NextCommand.Execute(null);
            var written = new TaskCompletionSource<bool>();
            wizard.Commit = _ => written.Task;
            var saving = wizard.SaveCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();

            dialog.Close();
            Dispatcher.UIThread.RunJobs();

            Assert.True(dialog.IsVisible);
            written.SetResult(true);
            await saving;
            Dispatcher.UIThread.RunJobs();
            await showing.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal("wide", (await wizard.Completion)?.Saved?.Name);
        });
    }

    private static async Task WithDialog(Func<SceneWizardWindow, SceneWizardViewModel, Task, Task> test)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SceneWizardWindowTests).Assembly);
        await session.Dispatch(async () =>
        {
            var camera = Substitute.For<ICameraConnection>();
            camera.ConnectionString.Returns("visca://10.0.0.1:52381");
            camera.WhenConnectedChanged.Returns(new BehaviorSubject<bool>(true));
            var position = Substitute.For<ICameraPositionControl>();
            position.ReadPositionAsync(Arg.Any<CancellationToken>())
                .Returns(new CameraPosition(new PanTiltPosition(5, 6), 700, 80));
            camera.PositionControl.Returns(position);
            using var wizard = new SceneWizardViewModel(new Localizer(), editing: false, [], new Dictionary<int, ICameraConnection> { [1] = camera });
            var owner = new Window();
            owner.Show();
            try
            {
                var showing = SceneWizardWindow.ShowAsync(owner, wizard, (_, _) => Task.FromResult(false));
                Dispatcher.UIThread.RunJobs();
                await test(owner.OwnedWindows.OfType<SceneWizardWindow>().Single(), wizard, showing);
            }
            finally
            {
                owner.Close();
            }

            // Without a result the lambda binds to Dispatch<Task>, which never awaits it.
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
