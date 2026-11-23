using System.Reactive.Subjects;
using Cgf.CameraControl.App.Configuration;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.App.Scenes;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Scenes;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;
using NSubstitute;

namespace Cgf.CameraControl.App.Tests;

public class SceneWizardTests
{
    private static readonly CameraPosition Here = new(new PanTiltPosition(5, 6), 700, 80);
    private static readonly CameraPosition Stored = new(new PanTiltPosition(1, 2), 300, null);

    private readonly Dictionary<int, ICameraConnection> _cameras = [];
    private readonly Dictionary<int, ICameraPositionControl> _positions = [];

    public class Probing : SceneWizardTests
    {
        [Fact]
        public async Task OffersOnlyThePartsTheCameraAnswered()
        {
            Camera(1, new CameraPosition(new PanTiltPosition(0, 0), 0, null));
            using var wizard = New();

            await wizard.ProbeAsync();

            var camera = Assert.Single(wizard.Cameras);
            Assert.True(camera.CanFrame);
            Assert.True(camera.SupportsPanTilt);
            Assert.True(camera.SupportsZoom);
            Assert.False(camera.SupportsFocus);
        }

        [Fact]
        public async Task ACameraThatAnswersNothingCannotBeFramedOrIncluded()
        {
            Camera(1, CameraPosition.None);
            using var wizard = New();

            await wizard.ProbeAsync();

            Assert.False(wizard.Cameras[0].CanFrame);
            Assert.False(wizard.CanSave);
        }

        [Fact]
        public void ACameraWithoutPositionSupportIsListedButNotProbed()
        {
            var camera = Substitute.For<ICameraConnection>();
            camera.ConnectionString.Returns("lanc");
            camera.PositionControl.Returns((ICameraPositionControl?)null);
            _cameras[9] = camera;
            using var wizard = New();

            Assert.Equal(PositionProbe.Unsupported, Assert.Single(wizard.Cameras).Probe);
        }

        [Fact]
        public async Task NothingCanBeSavedWhileACameraIsStillBeingChecked()
        {
            Camera(1, Here);
            Camera(2, Here);
            var answer = new TaskCompletionSource<CameraPosition>();
            _positions[2].ReadPositionAsync(Arg.Any<CancellationToken>()).Returns(answer.Task);
            using var wizard = New();
            wizard.Name = "a";
            var probing = wizard.ProbeAsync();
            wizard.Cameras[0].Lens(1, 0);
            wizard.NextCommand.Execute(null);

            Assert.False(wizard.CanSave);

            answer.SetResult(Here);
            await probing;

            Assert.True(wizard.CanSave);
        }
    }

    public class Saving : SceneWizardTests
    {
        [Fact]
        public async Task CapturesWhatWasMovedAndIncluded()
        {
            Camera(1, Here);
            using var wizard = New();
            await wizard.ProbeAsync();
            wizard.Name = " wide ";
            wizard.Cameras[0].Move(0.5, 0);
            wizard.Cameras[0].Move(0, 0);
            wizard.NextCommand.Execute(null);

            var outcome = await wizard.BuildAsync();

            Assert.NotNull(outcome);
            Assert.Equal("wide", outcome.Saved!.Name);
            Assert.Equal(new CameraPosition(Here.PanTilt, null, null), outcome.Saved.Cameras[1].Position);
            Assert.Null(outcome.Replaced);
        }

        [Fact]
        public async Task KeepsAStoredPartThatWasNotMoved()
        {
            Camera(1, Here);
            using var wizard = Edit(new CameraScene("sermon", new Dictionary<int, ScenePosition> { [1] = ScenePosition.From(Stored) }));
            await wizard.ProbeAsync();
            wizard.Cameras[0].Lens(1, 0);
            wizard.Cameras[0].Lens(0, 0);
            wizard.NextCommand.Execute(null);

            var outcome = await wizard.BuildAsync();

            Assert.Equal(new CameraPosition(Stored.PanTilt, Here.Zoom, null), outcome!.Saved!.Cameras[1].Position);
            Assert.Equal("sermon", outcome.Replaced);
        }

        [Fact]
        public async Task APartLeftOutIsNotStored()
        {
            Camera(1, Here);
            using var wizard = Edit(new CameraScene("sermon", new Dictionary<int, ScenePosition> { [1] = ScenePosition.From(Stored) }));
            await wizard.ProbeAsync();
            wizard.NextCommand.Execute(null);
            wizard.Cameras[0].StoreZoom = false;

            var outcome = await wizard.BuildAsync();

            Assert.Equal(new CameraPosition(Stored.PanTilt, null, null), outcome!.Saved!.Cameras[1].Position);
        }

        [Fact]
        public async Task StartingFromAnotherSceneCopiesItsFramings()
        {
            Camera(1, Here);
            var scenes = new[] { new CameraScene("sermon", new Dictionary<int, ScenePosition> { [1] = ScenePosition.From(Stored) }) };
            using var wizard = new SceneWizardViewModel(new Localizer(), editing: false, scenes, _cameras);
            await wizard.ProbeAsync();
            wizard.Name = "sermon close";
            wizard.SelectedBase = wizard.BaseChoices[1];
            wizard.NextCommand.Execute(null);

            var outcome = await wizard.BuildAsync();

            Assert.Equal(Stored, outcome!.Saved!.Cameras[1].Position);
            Assert.Null(outcome.Replaced);
        }

        [Fact]
        public async Task ACameraThatStopsAnsweringBlocksTheSaveWithItsName()
        {
            Camera(1, Here);
            using var wizard = New();
            await wizard.ProbeAsync();
            wizard.Name = "wide";
            wizard.Cameras[0].Lens(1, 0);
            wizard.NextCommand.Execute(null);
            _positions[1].ReadPositionAsync(Arg.Any<CancellationToken>()).Returns(CameraPosition.None);

            Assert.Null(await wizard.BuildAsync());
            Assert.Contains("camera-1", wizard.SaveError, StringComparison.Ordinal);
        }

        [Fact]
        public async Task BackKeepsAPartTheOperatorUnticked()
        {
            Camera(1, Here);
            using var wizard = New();
            await wizard.ProbeAsync();
            wizard.Name = "wide";
            wizard.Cameras[0].Lens(1, 0);
            wizard.NextCommand.Execute(null);
            wizard.Cameras[0].StoreZoom = false;

            wizard.BackCommand.Execute(null);
            wizard.NextCommand.Execute(null);

            Assert.False(wizard.Cameras[0].StoreZoom);
        }

        [Fact]
        public async Task StartingFromASceneKeepsItsCamerasThatAreNotConfigured()
        {
            Camera(1, Here);
            var scenes = new[]
            {
                new CameraScene("sermon", new Dictionary<int, ScenePosition> { [1] = ScenePosition.From(Stored), [9] = ScenePosition.From(Stored) }),
            };
            using var wizard = new SceneWizardViewModel(new Localizer(), editing: false, scenes, _cameras);
            await wizard.ProbeAsync();
            wizard.Name = "copy";
            wizard.SelectedBase = wizard.BaseChoices[1];
            wizard.NextCommand.Execute(null);

            var outcome = await wizard.BuildAsync();

            Assert.Equal(Stored, outcome!.Saved!.Cameras[9].Position);
        }

        [Fact]
        public async Task AFailedWriteKeepsTheWizardOpenWithItsChoices()
        {
            Camera(1, Here);
            using var wizard = New();
            await wizard.ProbeAsync();
            wizard.Name = "wide";
            wizard.Cameras[0].Lens(1, 0);
            wizard.NextCommand.Execute(null);
            wizard.Commit = _ => Task.FromResult(false);

            await wizard.SaveCommand.ExecuteAsync(null);

            Assert.False(wizard.IsFinished);
            Assert.NotNull(wizard.SaveError);
            Assert.True(wizard.Cameras[0].StoreZoom);
        }

        [Fact]
        public async Task ClosingWhileTheCamerasAnswerStoresNothing()
        {
            Camera(1, Here);
            var wizard = New();
            await wizard.ProbeAsync();
            wizard.Name = "wide";
            wizard.Cameras[0].Lens(1, 0);
            wizard.NextCommand.Execute(null);
            _positions[1].ReadPositionAsync(Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var answer = new TaskCompletionSource<CameraPosition>();
                    call.Arg<CancellationToken>().Register(() => answer.TrySetCanceled());
                    return answer.Task;
                });

            var saving = wizard.SaveCommand.ExecuteAsync(null);
            wizard.Dispose();
            await saving;

            Assert.Null(await wizard.Completion);
        }

        [Fact]
        public async Task LeavingWhileTheCamerasAnswerWritesNothing()
        {
            Camera(1, Here);
            var wizard = New();
            await wizard.ProbeAsync();
            wizard.Name = "wide";
            wizard.Cameras[0].Lens(1, 0);
            wizard.NextCommand.Execute(null);
            var answer = new TaskCompletionSource<CameraPosition>();
            _positions[1].ReadPositionAsync(Arg.Any<CancellationToken>()).Returns(answer.Task);
            var commits = 0;
            wizard.Commit = _ =>
            {
                commits++;
                return Task.FromResult(true);
            };

            var saving = wizard.SaveCommand.ExecuteAsync(null);
            wizard.Cancel();
            answer.SetResult(Here);
            await saving;

            Assert.Equal(0, commits);
            Assert.Null(await wizard.Completion);
            wizard.Dispose();
        }

        [Fact]
        public async Task CancelWhileStoringReportsTheStoredScene()
        {
            Camera(1, Here);
            var wizard = New();
            await wizard.ProbeAsync();
            wizard.Name = "wide";
            wizard.Cameras[0].Lens(1, 0);
            wizard.NextCommand.Execute(null);
            var written = new TaskCompletionSource<bool>();
            wizard.Commit = _ => written.Task;

            var saving = wizard.SaveCommand.ExecuteAsync(null);
            wizard.Cancel();
            written.SetResult(true);
            await saving;

            Assert.Equal("wide", (await wizard.Completion)?.Saved?.Name);
            wizard.Dispose();
        }

        [Fact]
        public async Task ACameraThatDidNotAnswerKeepsItsStoredFraming()
        {
            Camera(1, Here);
            Camera(2, CameraPosition.None);
            using var wizard = Edit(new CameraScene(
                "sermon",
                new Dictionary<int, ScenePosition> { [1] = ScenePosition.From(Stored), [2] = ScenePosition.From(Stored) }));
            await wizard.ProbeAsync();
            wizard.Name = "preach";
            wizard.NextCommand.Execute(null);

            var outcome = await wizard.BuildAsync();

            Assert.Equal(Stored, outcome!.Saved!.Cameras[2].Position);
        }

        [Fact]
        public async Task ASceneWithOnlyUnconfiguredCamerasCanBeRenamed()
        {
            Camera(1, Here);
            using var wizard = Edit(new CameraScene("sermon", new Dictionary<int, ScenePosition> { [9] = ScenePosition.From(Stored) }));
            await wizard.ProbeAsync();
            wizard.Name = "preach";
            wizard.NextCommand.Execute(null);

            Assert.True(wizard.CanSave);
            var outcome = await wizard.BuildAsync();

            Assert.Equal(Stored, outcome!.Saved!.Cameras[9].Position);
        }

        [Fact]
        public void ANewSceneWithNothingToStoreCannotBeSaved()
        {
            Camera(1, Here);
            using var wizard = New();
            wizard.Name = "wide";
            wizard.NextCommand.Execute(null);

            Assert.False(wizard.CanSave);
        }

        [Fact]
        public async Task KeepsAStoredPartTheCameraDoesNotAnswer()
        {
            Camera(1, new CameraPosition(null, 700, null));
            using var wizard = Edit(new CameraScene("sermon", new Dictionary<int, ScenePosition> { [1] = ScenePosition.From(Stored) }));
            await wizard.ProbeAsync();
            wizard.Name = "preach";
            wizard.NextCommand.Execute(null);

            var outcome = await wizard.BuildAsync();

            Assert.Equal(Stored, outcome!.Saved!.Cameras[1].Position);
        }

        [Fact]
        public async Task MovingAStoredPartTheCameraDoesNotAnswerBlocksTheSave()
        {
            Camera(1, new CameraPosition(null, 700, null));
            using var wizard = Edit(new CameraScene("sermon", new Dictionary<int, ScenePosition> { [1] = ScenePosition.From(Stored) }));
            await wizard.ProbeAsync();
            wizard.Cameras[0].Move(1, 0);
            wizard.NextCommand.Execute(null);

            Assert.Null(await wizard.BuildAsync());
            Assert.Contains("camera-1", wizard.SaveError, StringComparison.Ordinal);
        }

        [Fact]
        public void ANameAnotherSceneHasCannotBeUsed()
        {
            using var wizard = new SceneWizardViewModel(new Localizer(), editing: false,
                [new CameraScene("Sermon", new Dictionary<int, ScenePosition>())],
                _cameras);

            wizard.Name = "sermon";

            Assert.False(wizard.CanNext);
            Assert.NotNull(wizard.NameError);
        }

        [Fact]
        public void TheEditedSceneMayKeepItsName()
        {
            using var wizard = Edit(new CameraScene("sermon", new Dictionary<int, ScenePosition>()));

            Assert.Equal("sermon", wizard.Name);
            Assert.True(wizard.CanNext);
        }
    }

    public class Leaving : SceneWizardTests
    {
        [Fact]
        public async Task ClosingStopsEveryCameraThatCouldBeDriven()
        {
            Camera(1, Here);
            var wizard = New();
            await wizard.ProbeAsync();
            wizard.Cameras[0].Move(1, 0);

            wizard.Dispose();

            _cameras[1].Received().Pan(0);
            _cameras[1].Received().Zoom(0);
        }
    }

    public class References
    {
        private static RootConfig Desk() => ConfigLoader.Load(
            """
            { "interfaces": [ { "type": "keyboard", "instance": 1, "videoMixer": 1, "cameraMap": {}, "keys": {},
                "connectionChange": { "type": "direct", "default": {} },
                "functions": {
                  "a": { "type": "scene", "name": "Sermon" },
                  "b": { "type": "key", "index": 1 },
                  "c": { "type": "scene", "name": "wide" } } } ] }
            """,
            out _);

        [Fact]
        public void ListsTheFunctionsThatRecallAScene()
        {
            Assert.Equal(["keyboard[1]: a"], SceneReferences.To(Desk(), "sermon"));
        }

        [Fact]
        public void TheConfigurationEditorKeepsTheScenes()
        {
            var config = Desk() with
            {
                Scenes = [new CameraScene("sermon", new Dictionary<int, ScenePosition> { [1] = ScenePosition.From(Stored) })],
            };

            var saved = Cgf.CameraControl.App.Editing.ConfigDraft.From(new Localizer(), config).ToConfig();

            Assert.Equal("sermon", Assert.Single(saved.Scenes).Name);
        }

        [Fact]
        public void RenamingRepointsOnlyThoseFunctions()
        {
            var (config, changed) = SceneReferences.Rename(Desk(), "sermon", "preach");

            Assert.Equal(1, changed);
            var functions = Assert.IsType<KeyboardConfiguration>(Assert.Single(config.Interfaces)).Functions;
            Assert.NotNull(functions);
            Assert.Equal("preach", Assert.IsType<SceneSpecialFunctionConfiguration>(functions["a"]).Name);
            Assert.Equal("wide", Assert.IsType<SceneSpecialFunctionConfiguration>(functions["c"]).Name);
            Assert.Equal(1, Assert.IsType<KeySpecialFunctionConfiguration>(functions["b"]).Index);
        }
    }

    private SceneWizardViewModel New() => new(new Localizer(), editing: false, [], _cameras);

    private SceneWizardViewModel Edit(CameraScene scene) => new(new Localizer(), editing: true, [scene], _cameras);

    private void Camera(int instance, CameraPosition answer)
    {
        var camera = Substitute.For<ICameraConnection>();
        var position = Substitute.For<ICameraPositionControl>();
        camera.ConnectionString.Returns($"camera-{instance}");
        camera.WhenConnectedChanged.Returns(new BehaviorSubject<bool>(true));
        camera.PositionControl.Returns(position);
        position.ReadPositionAsync(Arg.Any<CancellationToken>()).Returns(answer);
        _cameras[instance] = camera;
        _positions[instance] = position;
    }
}
