using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.Configuration.Converters;

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
    public IReadOnlyDictionary<ButtonDirection, int> Default { get; set; } = null!;

    public IReadOnlyDictionary<ButtonDirection, int>? Alt { get; set; }

    public IReadOnlyDictionary<ButtonDirection, int>? AltLower { get; set; }
}

/// Each direction steps from the current input to a neighbouring one.
public sealed record DirectionalConnectionChangeConfiguration : ConnectionChangeConfiguration
{
    [JsonRequired]
    public IReadOnlyDictionary<int, IReadOnlyDictionary<ButtonDirection, int>> Directions { get; set; } = null!;
}

/// specialFunctionDefinitionConfigurationSchema and its four refinements.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KeySpecialFunctionConfiguration), "key")]
[JsonDerivedType(typeof(MacroLoopSpecialFunctionConfiguration), "macroLoop")]
[JsonDerivedType(typeof(ConnectionChangeSpecialFunctionConfiguration), "connectionChange")]
[JsonDerivedType(typeof(MacroToggleSpecialFunctionConfiguration), "macroToggle")]
public abstract record SpecialFunctionConfiguration;

public sealed record KeySpecialFunctionConfiguration : SpecialFunctionConfiguration
{
    [JsonConverter(typeof(PositiveIntConverter))]
    [JsonRequired]
    public int Index { get; set; }
}

public sealed record ConnectionChangeSpecialFunctionConfiguration : SpecialFunctionConfiguration
{
    [JsonConverter(typeof(PositiveIntConverter))]
    [JsonRequired]
    public int Index { get; set; }
}

public sealed record MacroLoopSpecialFunctionConfiguration : SpecialFunctionConfiguration
{
    [JsonRequired]
    public IReadOnlyList<int> Indexes { get; set; } = null!;
}

public sealed record MacroToggleSpecialFunctionConfiguration : SpecialFunctionConfiguration
{
    [JsonConverter(typeof(NonNegativeIntConverter))]
    [JsonRequired]
    public int IndexOn { get; set; }

    [JsonConverter(typeof(NonNegativeIntConverter))]
    [JsonRequired]
    public int IndexOff { get; set; }

    [JsonRequired]
    public MacroToggleConditionConfiguration Condition { get; set; } = null!;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KeyConditionConfiguration), "key")]
[JsonDerivedType(typeof(AuxSelectionConditionConfiguration), "aux_selection")]
public abstract record MacroToggleConditionConfiguration;

public sealed record KeyConditionConfiguration : MacroToggleConditionConfiguration
{
    [JsonConverter(typeof(NonNegativeIntConverter))]
    [JsonRequired]
    public int Key { get; set; }
}

public sealed record AuxSelectionConditionConfiguration : MacroToggleConditionConfiguration
{
    [JsonConverter(typeof(NonNegativeIntConverter))]
    [JsonRequired]
    public int Aux { get; set; }

    [JsonConverter(typeof(NonNegativeIntConverter))]
    [JsonRequired]
    public int Selection { get; set; }
}
