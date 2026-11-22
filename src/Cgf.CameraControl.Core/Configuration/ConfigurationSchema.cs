using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Cgf.CameraControl.Core.Configuration;

/// Which record each type string of each section reads as, over a source-generated context that
/// holds the metadata of every one of them.
///
/// Core cannot name the plugins' records, so the section bases carry no [JsonDerivedType]. The
/// application registers each record here instead, and the registrations become the section bases'
/// polymorphism through a contract modifier on the generated resolver. Nothing is found by
/// reflection: every type registered has to be in the context, and the published binary keeps what
/// the context names.
public sealed class ConfigurationSchema
{
    private readonly JsonSerializerContext _context;
    private readonly List<JsonDerivedType> _cameras = [];
    private readonly List<JsonDerivedType> _mixers = [];
    private readonly List<JsonDerivedType> _interfaces = [];
    private JsonSerializerOptions? _options;

    public ConfigurationSchema(JsonSerializerContext context) => _context = context;

    /// The options the loader reads with and the writer writes with. Built on first use, after which
    /// the schema takes no more registrations.
    public JsonSerializerOptions Options => _options ??= Build();

    public ConfigurationSchema Camera<T>(string type)
        where T : CameraConfiguration => Register(_cameras, typeof(T), type);

    public ConfigurationSchema VideoMixer<T>(string type)
        where T : VideoMixerConfiguration => Register(_mixers, typeof(T), type);

    public ConfigurationSchema Interface<T>(string type)
        where T : InterfaceConfiguration => Register(_interfaces, typeof(T), type);

    /// The registered type strings of a section, as the file has to spell them apart from case.
    public IReadOnlyList<string> TypesOf<TSection>()
        where TSection : EntryConfiguration =>
        [.. Section(typeof(TSection)).Select(derived => (string)derived.TypeDiscriminator!)];

    private ConfigurationSchema Register(List<JsonDerivedType> section, Type type, string name)
    {
        if (_options is not null)
        {
            throw new InvalidOperationException("the configuration schema is already in use");
        }

        // A discriminator is matched without regard to case when the file is read, so two that differ
        // only in case could not be told apart.
        if (section.Any(derived => string.Equals((string)derived.TypeDiscriminator!, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"configuration type '{name}' is registered twice");
        }

        section.Add(new JsonDerivedType(type, name));
        return this;
    }

    private List<JsonDerivedType> Section(Type type) =>
        type == typeof(CameraConfiguration) ? _cameras
        : type == typeof(VideoMixerConfiguration) ? _mixers
        : type == typeof(InterfaceConfiguration) ? _interfaces
        : throw new ArgumentOutOfRangeException(nameof(type), type, "not a configuration section");

    /// Unknown properties are refused at every depth, so a misspelt key is reported against its path
    /// rather than silently doing nothing, and `type` may stand anywhere among an entry's properties.
    private JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions(_context.Options)
        {
            TypeInfoResolver = _context
                .WithAddedModifier(AddSections)
                .WithAddedModifier(ConfigurationDefaults.OmitWhenDefault),
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true,
            AllowOutOfOrderMetadataProperties = true,
        };

        options.MakeReadOnly();
        return options;
    }

    private void AddSections(JsonTypeInfo info)
    {
        if ((info.Type != typeof(CameraConfiguration) &&
             info.Type != typeof(VideoMixerConfiguration) &&
             info.Type != typeof(InterfaceConfiguration)) ||
            Section(info.Type).Count == 0)
        {
            return;
        }

        var polymorphism = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "type",
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };

        foreach (var derived in Section(info.Type))
        {
            polymorphism.DerivedTypes.Add(derived);
        }

        info.PolymorphismOptions = polymorphism;
    }
}
