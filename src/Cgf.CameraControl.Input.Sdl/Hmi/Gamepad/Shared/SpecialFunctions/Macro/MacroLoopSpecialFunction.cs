using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.SpecialFunctions.Macro;

/// Steps through the configured macros one press at a time and wraps at the end.
public sealed class MacroLoopSpecialFunction(MacroLoopSpecialFunctionConfiguration config) : ISpecialFunction
{
    private int _nextIndex;

    public Task RunAsync(IVideoMixer mixer, CancellationToken cancellationToken)
    {
        mixer.RunMacro(config.Indexes[_nextIndex]);
        _nextIndex = (_nextIndex + 1) % config.Indexes.Count;
        return Task.CompletedTask;
    }
}
