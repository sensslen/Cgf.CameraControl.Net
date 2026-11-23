using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

/// connectionChangeConfigurationSchema and its two refinements.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(DirectConnectionChangeConfiguration), "direct")]
[JsonDerivedType(typeof(DirectionalConnectionChangeConfiguration), "directional")]
public abstract record ConnectionChangeConfiguration;

/// Each direction selects a fixed input, with a separate set per modifier.
public sealed record DirectConnectionChangeConfiguration : ConnectionChangeConfiguration
{
    [JsonRequired]
    public IReadOnlyDictionary<ButtonDirection, int> Default { get; set; } = new Dictionary<ButtonDirection, int>();

    public IReadOnlyDictionary<ButtonDirection, int>? Alt { get; set; }

    public IReadOnlyDictionary<ButtonDirection, int>? AltLower { get; set; }
}

/// Each direction steps from the current input to a neighbouring one.
public sealed record DirectionalConnectionChangeConfiguration : ConnectionChangeConfiguration
{
    [JsonRequired]
    public IReadOnlyDictionary<int, IReadOnlyDictionary<ButtonDirection, int>> Directions { get; set; } = null!;
}

/// specialFunctionDefinitionConfigurationSchema and its four refinements, plus scene recall.
///
/// The generated validators check a member by its declared type, so they cannot follow these
/// polymorphic records; each one validates itself, and its holder calls it.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KeySpecialFunctionConfiguration), "key")]
[JsonDerivedType(typeof(MacroLoopSpecialFunctionConfiguration), "macroLoop")]
[JsonDerivedType(typeof(ConnectionChangeSpecialFunctionConfiguration), "connectionChange")]
[JsonDerivedType(typeof(MacroToggleSpecialFunctionConfiguration), "macroToggle")]
[JsonDerivedType(typeof(SceneSpecialFunctionConfiguration), "scene")]
public abstract record SpecialFunctionConfiguration
{
    public abstract IEnumerable<ValueProblem> Validate();
}

/// Recalls a scene by name. The scene is looked up when the button is pressed, so one saved after
/// the configuration was loaded is recalled without a reload.
public sealed record SceneSpecialFunctionConfiguration : SpecialFunctionConfiguration
{
    /// Scenes are saved from the window after the file is loaded, so the name is only checked for
    /// being one at all.
    [JsonRequired]
    [Required]
    public string Name { get; set; } = null!;

    public override IEnumerable<ValueProblem> Validate() => ValueProblem.Of(new SceneSpecialFunctionConfigurationValidator(), this);
}

public sealed record KeySpecialFunctionConfiguration : SpecialFunctionConfiguration
{
    [JsonRequired]
    [Range(1, int.MaxValue)]
    public int Index { get; set; }

    public override IEnumerable<ValueProblem> Validate() => ValueProblem.Of(new KeySpecialFunctionConfigurationValidator(), this);
}

public sealed record ConnectionChangeSpecialFunctionConfiguration : SpecialFunctionConfiguration
{
    [JsonRequired]
    [Range(1, int.MaxValue)]
    public int Index { get; set; }

    public override IEnumerable<ValueProblem> Validate() =>
        ValueProblem.Of(new ConnectionChangeSpecialFunctionConfigurationValidator(), this);
}

public sealed record MacroLoopSpecialFunctionConfiguration : SpecialFunctionConfiguration
{
    /// An empty loop is a button that logs an error on every press instead of running a macro.
    [JsonRequired]
    [NotEmpty]
    [NonNegativeNumbers]
    public IReadOnlyList<int> Indexes { get; set; } = null!;

    public override IEnumerable<ValueProblem> Validate() => ValueProblem.Of(new MacroLoopSpecialFunctionConfigurationValidator(), this);
}

public sealed record MacroToggleSpecialFunctionConfiguration : SpecialFunctionConfiguration
{
    [JsonRequired]
    [Range(0, int.MaxValue)]
    public int IndexOn { get; set; }

    [JsonRequired]
    [Range(0, int.MaxValue)]
    public int IndexOff { get; set; }

    [JsonRequired]
    public MacroToggleConditionConfiguration Condition { get; set; } = null!;

    public override IEnumerable<ValueProblem> Validate() =>
        ValueProblem.Of(new MacroToggleSpecialFunctionConfigurationValidator(), this)
            .Concat(Condition.Validate().Select(problem => problem.Under("condition")));
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KeyConditionConfiguration), "key")]
[JsonDerivedType(typeof(AuxSelectionConditionConfiguration), "aux_selection")]
public abstract record MacroToggleConditionConfiguration
{
    public abstract IEnumerable<ValueProblem> Validate();
}

public sealed record KeyConditionConfiguration : MacroToggleConditionConfiguration
{
    [JsonRequired]
    [Range(0, int.MaxValue)]
    public int Key { get; set; }

    public override IEnumerable<ValueProblem> Validate() => ValueProblem.Of(new KeyConditionConfigurationValidator(), this);
}

public sealed record AuxSelectionConditionConfiguration : MacroToggleConditionConfiguration
{
    [JsonRequired]
    [Range(0, int.MaxValue)]
    public int Aux { get; set; }

    [JsonRequired]
    [Range(0, int.MaxValue)]
    public int Selection { get; set; }

    public override IEnumerable<ValueProblem> Validate() => ValueProblem.Of(new AuxSelectionConditionConfigurationValidator(), this);
}

[OptionsValidator]
internal sealed partial class SceneSpecialFunctionConfigurationValidator : IValidateOptions<SceneSpecialFunctionConfiguration>;

[OptionsValidator]
internal sealed partial class KeySpecialFunctionConfigurationValidator : IValidateOptions<KeySpecialFunctionConfiguration>;

[OptionsValidator]
internal sealed partial class ConnectionChangeSpecialFunctionConfigurationValidator
    : IValidateOptions<ConnectionChangeSpecialFunctionConfiguration>;

[OptionsValidator]
internal sealed partial class MacroLoopSpecialFunctionConfigurationValidator : IValidateOptions<MacroLoopSpecialFunctionConfiguration>;

[OptionsValidator]
internal sealed partial class MacroToggleSpecialFunctionConfigurationValidator : IValidateOptions<MacroToggleSpecialFunctionConfiguration>;

[OptionsValidator]
internal sealed partial class KeyConditionConfigurationValidator : IValidateOptions<KeyConditionConfiguration>;

[OptionsValidator]
internal sealed partial class AuxSelectionConditionConfigurationValidator : IValidateOptions<AuxSelectionConditionConfiguration>;
