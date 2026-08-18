using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.SpecialFunctions.Macro;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.SpecialFunctions.Macro.Toggle;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.SpecialFunctions;

public static class SpecialFunctionFactory
{
    public static ISpecialFunction Get(SpecialFunctionConfiguration config) => config switch
    {
        KeySpecialFunctionConfiguration key => new KeySpecialFunction(key),
        ConnectionChangeSpecialFunctionConfiguration change => new ConnectionChangeSpecialFunction(change),
        MacroLoopSpecialFunctionConfiguration loop => new MacroLoopSpecialFunction(loop),
        MacroToggleSpecialFunctionConfiguration toggle => new MacroToggleSpecialFunction(
            toggle,
            MacroToggleSpecialFunctionConditionFactory.Get(toggle.Condition)),
        _ => throw new ArgumentOutOfRangeException(nameof(config), config, "unhandled special function"),
    };
}
