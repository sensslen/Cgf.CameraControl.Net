using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.SpecialFunctions.Macro.Toggle;

public interface IMacroToggleSpecialFunctionCondition
{
    Task<bool> IsActiveAsync(IVideoMixer mixer, CancellationToken cancellationToken);
}

/// Runs one of two macros depending on whether the condition currently holds, so a single button
/// both arms and disarms whatever the pair of macros sets up.
public sealed class MacroToggleSpecialFunction(
    MacroToggleSpecialFunctionConfiguration config,
    IMacroToggleSpecialFunctionCondition condition) : ISpecialFunction
{
    public async Task RunAsync(IVideoMixer mixer, CancellationToken cancellationToken)
    {
        bool isActive;
        try
        {
            isActive = await condition.IsActiveAsync(mixer, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A switcher that cannot answer leaves the toggle undecidable, and guessing would run
            // the wrong macro on air.
            return;
        }

        mixer.RunMacro(isActive ? config.IndexOff : config.IndexOn);
    }
}

public sealed class MacroToggleSpecialFunctionConditionKey(KeyConditionConfiguration config)
    : IMacroToggleSpecialFunctionCondition
{
    public Task<bool> IsActiveAsync(IVideoMixer mixer, CancellationToken cancellationToken) =>
        mixer.IsKeySetAsync(config.Key, cancellationToken);
}

public sealed class MacroToggleSpecialFunctionConditionAuxSelection(AuxSelectionConditionConfiguration config)
    : IMacroToggleSpecialFunctionCondition
{
    public async Task<bool> IsActiveAsync(IVideoMixer mixer, CancellationToken cancellationToken) =>
        await mixer.GetAuxiliarySelectionAsync(config.Aux, cancellationToken).ConfigureAwait(false) == config.Selection;
}

public static class MacroToggleSpecialFunctionConditionFactory
{
    public static IMacroToggleSpecialFunctionCondition Get(MacroToggleConditionConfiguration config) => config switch
    {
        KeyConditionConfiguration key => new MacroToggleSpecialFunctionConditionKey(key),
        AuxSelectionConditionConfiguration aux => new MacroToggleSpecialFunctionConditionAuxSelection(aux),
        _ => throw new ArgumentOutOfRangeException(nameof(config), config, "unhandled macro toggle condition"),
    };
}
