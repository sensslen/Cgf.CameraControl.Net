using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.SpecialFunctions;

/// The interface does the recalling, because it is what knows which camera is on program.
public sealed class SceneSpecialFunction(SceneSpecialFunctionConfiguration config, Action<string> recall) : ISpecialFunction
{
    public Task RunAsync(IVideoMixer mixer, CancellationToken cancellationToken)
    {
        recall(config.Name);
        return Task.CompletedTask;
    }
}
