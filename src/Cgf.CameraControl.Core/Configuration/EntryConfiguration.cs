using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Cgf.CameraControl.Core.Configuration;

/// One entry of one section: the type that picks its builder, and the instance the rest of the
/// configuration refers to it by.
///
/// Core knows only the three section bases below. Each plugin derives its own records from them, and
/// the application, the one project that references every plugin, reads and writes them.
///
/// Two System.Text.Json rules shape how every configuration record in this solution is written, and
/// breaking either one produces wrong values rather than an error.
///
/// Properties are declared `get; set;`, never `get; init;`. The source generator does not run
/// property initialisers for init-only properties, so `ShowTallyLight = true` would silently arrive
/// as false. Reflection-based serialization does honour them, so this only shows up once published.
///
/// Presence is enforced with [JsonRequired], never the C# `required` modifier. A type carrying a
/// required member cannot be constructed with `new T()`, which forces the same uninitialized path
/// and loses the defaults again.
///
/// System.Text.Json checks shape, not values. A value rule is a DataAnnotations attribute on the
/// property, and each record enforces its own through an [OptionsValidator] the plugin declares
/// beside it, because Core cannot name the plugins' records. The loader and the editor both run it.
public abstract record EntryConfiguration
{
    protected EntryConfiguration(string type) => Type = type;

    /// Written as the discriminator rather than as a property, so it is not serialized from here.
    [JsonIgnore]
    public string Type { get; }

    [JsonRequired]
    [JsonPropertyOrder(-1)]
    [Range(1, int.MaxValue)]
    public int Instance { get; set; }

    public abstract IEnumerable<ValueProblem> Validate();

    /// How errors name the entry, as `viscaoverip[7]`.
    public sealed override string ToString() => $"{Type}[{Instance}]";
}

public abstract record CameraConfiguration(string Type) : EntryConfiguration(Type);

public abstract record VideoMixerConfiguration(string Type) : EntryConfiguration(Type);

public abstract record InterfaceConfiguration(string Type) : EntryConfiguration(Type);

/// An entry the loader could not read: not an object, without a type or an instance, of an unknown
/// type, or of a shape or a value its type refuses. It is reported, never built, and written back
/// exactly as it was read, so a file is never shortened by opening and saving it.
public interface IInvalidConfiguration
{
    /// The entry as it appeared in the file, without comments.
    string Json { get; }

    /// Why it could not be read, as the loader reported it.
    string Problem { get; }
}

/// The instance is the one the file wrote, or 0 when it wrote none that could be read.
public sealed record InvalidCameraConfiguration(string Type, string Json, string Problem)
    : CameraConfiguration(Type), IInvalidConfiguration
{
    public override IEnumerable<ValueProblem> Validate() => [];
}

public sealed record InvalidVideoMixerConfiguration(string Type, string Json, string Problem)
    : VideoMixerConfiguration(Type), IInvalidConfiguration
{
    public override IEnumerable<ValueProblem> Validate() => [];
}

public sealed record InvalidInterfaceConfiguration(string Type, string Json, string Problem)
    : InterfaceConfiguration(Type), IInvalidConfiguration
{
    public override IEnumerable<ValueProblem> Validate() => [];
}
