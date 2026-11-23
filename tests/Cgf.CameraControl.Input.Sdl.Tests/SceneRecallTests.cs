using System.Reactive.Subjects;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Core.Scenes;
using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;
using NSubstitute;

namespace Cgf.CameraControl.Input.Sdl.Tests;

public class SceneRecallTests
{
    private static readonly CameraPosition Wide = new(new PanTiltPosition(100, -20), 0, null);
    private static readonly CameraPosition Close = new(null, 4000, 300);

    private readonly FakeGamepadDevice _device = new();
    private readonly IVideoMixer _mixer = Substitute.For<IVideoMixer>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly Subject<PreviewChange> _preview = new();
    private readonly Subject<int> _program = new();
    private readonly Dictionary<int, ICameraConnection> _cameras = [];
    private readonly Dictionary<int, ICameraPositionControl> _positions = [];
    private readonly SceneLibrary _scenes = new();

    public SceneRecallTests()
    {
        _mixer.WhenPreviewChanged.Returns(_preview);
        _mixer.WhenProgramChanged.Returns(_program);
        _mixer.WhenConnectedChanged.Returns(new BehaviorSubject<bool>(true));
        _mixer.ConnectionString.Returns("mixer");
    }

    [Fact]
    public void MovesEveryStoredCameraToItsFraming()
    {
        Camera(1);
        Camera(2);
        _scenes.Replace([new CameraScene("sermon", new Dictionary<int, ScenePosition> { [1] = ScenePosition.From(Wide), [2] = ScenePosition.From(Close) })]);
        Build();

        _device.PressFace(ButtonDirection.Down);

        _positions[1].Received(1).MoveTo(Wide);
        _positions[2].Received(1).MoveTo(Close);
    }

    [Fact]
    public void LeavesTheCameraOnProgramWhereItIs()
    {
        Camera(1);
        Camera(2);
        _scenes.Replace([new CameraScene("sermon", new Dictionary<int, ScenePosition> { [1] = ScenePosition.From(Wide), [2] = ScenePosition.From(Close) })]);
        Build();
        _program.OnNext(2);

        _device.PressFace(ButtonDirection.Down);

        _positions[1].Received(1).MoveTo(Wide);
        _positions[2].DidNotReceive().MoveTo(Arg.Any<CameraPosition>());
    }

    /// Two desks share the cameras: one on air on the other desk's mixer is on air all the same.
    [Fact]
    public void LeavesACameraAloneThatAnotherInterfaceHasOnProgram()
    {
        Camera(1);
        _scenes.Replace([new CameraScene("sermon", new Dictionary<int, ScenePosition> { [1] = ScenePosition.From(Wide) })]);
        var otherProgram = new Subject<int>();
        var otherMixer = Substitute.For<IVideoMixer>();
        otherMixer.WhenPreviewChanged.Returns(new Subject<PreviewChange>());
        otherMixer.WhenProgramChanged.Returns(otherProgram);
        otherMixer.WhenConnectedChanged.Returns(new BehaviorSubject<bool>(true));
        _ = new Gamepad(Configuration(), new FakeGamepadDevice(), otherMixer, _cameras.GetValueOrDefault, _logger, scenes: _scenes);
        Build();
        otherProgram.OnNext(1);

        _device.PressFace(ButtonDirection.Down);

        _positions[1].DidNotReceive().MoveTo(Arg.Any<CameraPosition>());
    }

    [Fact]
    public void ACameraBackFromProgramIsMovedAgain()
    {
        Camera(1);
        Camera(2);
        _scenes.Replace([new CameraScene("sermon", new Dictionary<int, ScenePosition> { [1] = ScenePosition.From(Wide) })]);
        Build();
        _program.OnNext(1);
        _program.OnNext(2);

        _device.PressFace(ButtonDirection.Down);

        _positions[1].Received(1).MoveTo(Wide);
    }

    /// The library is read at the press, so a scene saved after the interface was built is recalled.
    [Fact]
    public void RecallsASceneSavedAfterTheInterfaceWasBuilt()
    {
        Camera(1);
        Build();
        _scenes.Replace([new CameraScene("SERMON", new Dictionary<int, ScenePosition> { [1] = ScenePosition.From(Wide) })]);

        _device.PressFace(ButtonDirection.Down);

        _positions[1].Received(1).MoveTo(Wide);
    }

    [Fact]
    public void AMissingSceneIsReportedAndMovesNothing()
    {
        Camera(1);
        Build();

        _device.PressFace(ButtonDirection.Down);

        _positions[1].DidNotReceive().MoveTo(Arg.Any<CameraPosition>());
        _logger.Received().Error("Gamepad", "no scene named sermon is saved");
    }

    [Fact]
    public void ACameraThatCannotMoveToPositionsIsReported()
    {
        var camera = Substitute.For<ICameraConnection>();
        camera.ConnectionString.Returns("lanc");
        camera.WhenConnectedChanged.Returns(new BehaviorSubject<bool>(true));
        camera.PositionControl.Returns((ICameraPositionControl?)null);
        _cameras[3] = camera;
        _scenes.Replace([new CameraScene("sermon", new Dictionary<int, ScenePosition> { [3] = ScenePosition.From(Wide) })]);
        Build();

        _device.PressFace(ButtonDirection.Down);

        _logger.Received().Error("Gamepad", Arg.Is<string>(message => message.Contains("cannot move to a stored position")));
    }

    private void Camera(int instance)
    {
        var camera = Substitute.For<ICameraConnection>();
        var position = Substitute.For<ICameraPositionControl>();
        camera.ConnectionString.Returns($"camera-{instance}");
        camera.WhenConnectedChanged.Returns(new BehaviorSubject<bool>(true));
        camera.PositionControl.Returns(position);
        _cameras[instance] = camera;
        _positions[instance] = position;
    }

    private GamepadConfiguration Configuration() => new()
    {
        VideoMixer = 1,
        CameraMap = _cameras.Keys.ToDictionary(key => key, key => key),
        ConnectionChange = new DirectConnectionChangeConfiguration
        {
            Default = new Dictionary<ButtonDirection, int>(),
        },
        Functions = new Dictionary<string, SpecialFunctionConfiguration>
        {
            ["sermon"] = new SceneSpecialFunctionConfiguration { Name = "sermon" },
        },
    };

    private void Build() => _ = new Gamepad(
        Configuration(),
        _device,
        _mixer,
        _cameras.GetValueOrDefault,
        _logger,
        new PadBindings { Default = new Dictionary<ButtonDirection, string> { [ButtonDirection.Down] = "sermon" } },
        scenes: _scenes);
}
