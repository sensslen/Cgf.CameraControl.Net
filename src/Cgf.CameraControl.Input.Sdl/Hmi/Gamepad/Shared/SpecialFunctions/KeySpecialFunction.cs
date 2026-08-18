using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.SpecialFunctions;

public sealed class KeySpecialFunction(KeySpecialFunctionConfiguration config) : ISpecialFunction
{
    public Task RunAsync(IVideoMixer mixer, CancellationToken cancellationToken)
    {
        mixer.ToggleKey(config.Index);
        return Task.CompletedTask;
    }
}

public sealed class ConnectionChangeSpecialFunction(ConnectionChangeSpecialFunctionConfiguration config) : ISpecialFunction
{
    public Task RunAsync(IVideoMixer mixer, CancellationToken cancellationToken)
    {
        mixer.ChangeInput(config.Index);
        return Task.CompletedTask;
    }
}
