using Cgf.CameraControl.Core.VideoMixer;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.SpecialFunctions;

public interface ISpecialFunction
{
    Task RunAsync(IVideoMixer mixer, CancellationToken cancellationToken);
}
