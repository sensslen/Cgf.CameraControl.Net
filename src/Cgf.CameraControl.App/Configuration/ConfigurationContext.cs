using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;
using Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;
using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;
using Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Scenes;
using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Core.VideoMixer.Passthrough;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.App.Configuration;

/// Unknown properties are refused at every depth, so a misspelt key is reported against its path
/// rather than silently doing nothing, and `type` may stand anywhere among an entry's properties.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    AllowOutOfOrderMetadataProperties = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(CameraConfiguration))]
[JsonSerializable(typeof(VideoMixerConfiguration))]
[JsonSerializable(typeof(InterfaceConfiguration))]
[JsonSerializable(typeof(WebsocketPtzLancCameraConfiguration))]
[JsonSerializable(typeof(SignalrPtzLancCameraConfiguration))]
[JsonSerializable(typeof(ViscaOverIpCameraConfiguration))]
[JsonSerializable(typeof(AtemConfiguration))]
[JsonSerializable(typeof(PassthroughConfiguration))]
[JsonSerializable(typeof(GamepadConfiguration))]
[JsonSerializable(typeof(LogitechF310Configuration))]
[JsonSerializable(typeof(LogitechF710Configuration))]
[JsonSerializable(typeof(LogitechRumblepad2Configuration))]
[JsonSerializable(typeof(KeyboardConfiguration))]
[JsonSerializable(typeof(CameraScene))]
[JsonSerializable(typeof(MixerTallyMode))]
[JsonSerializable(typeof(ViscaTallyMode))]
[JsonSerializable(typeof(GamepadBackend))]
[JsonSerializable(typeof(string))]
public sealed partial class ConfigurationContext : JsonSerializerContext;

/// The records each section reads as, and the contracts the loader and writer use with them attached.
public static class ConfigurationSections
{
    public static IReadOnlyList<JsonDerivedType> CameraTypes { get; } =
    [
        new(typeof(ViscaOverIpCameraConfiguration), ViscaOverIpCameraConfiguration.TypeName),
        new(typeof(WebsocketPtzLancCameraConfiguration), WebsocketPtzLancCameraConfiguration.TypeName),
        new(typeof(SignalrPtzLancCameraConfiguration), SignalrPtzLancCameraConfiguration.TypeName),
    ];

    public static IReadOnlyList<JsonDerivedType> VideoMixerTypes { get; } =
    [
        new(typeof(AtemConfiguration), AtemConfiguration.TypeName),
        new(typeof(PassthroughConfiguration), PassthroughConfiguration.TypeName),
    ];

    public static IReadOnlyList<JsonDerivedType> InterfaceTypes { get; } =
    [
        new(typeof(GamepadConfiguration), GamepadConfiguration.TypeName),
        new(typeof(LogitechF310Configuration), LogitechF310Configuration.TypeName),
        new(typeof(LogitechF710Configuration), LogitechF710Configuration.TypeName),
        new(typeof(LogitechRumblepad2Configuration), LogitechRumblepad2Configuration.TypeName),
        new(typeof(KeyboardConfiguration), KeyboardConfiguration.TypeName),
    ];

    public static JsonSerializerOptions Options { get; } = new(ConfigurationContext.Default.Options)
    {
        TypeInfoResolver = ConfigurationContext.Default.WithAddedModifier(AddSections),
    };

    public static JsonTypeInfo<CameraConfiguration> Cameras { get; } = Section<CameraConfiguration>();

    public static JsonTypeInfo<VideoMixerConfiguration> VideoMixers { get; } = Section<VideoMixerConfiguration>();

    public static JsonTypeInfo<InterfaceConfiguration> Interfaces { get; } = Section<InterfaceConfiguration>();

    // A context bound to these options would hand out typed contracts, but binding it replaces the
    // resolver the modifier sits on, so options only resolve a contract by its Type.
    private static JsonTypeInfo<T> Section<T>()
        where T : EntryConfiguration =>
        Options.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> info
            ? info
            : throw new InvalidOperationException($"no contract for {typeof(T).Name}");

    // [JsonDerivedType] only works on the base type itself and the section bases are Core's, so the
    // list is attached through the contract model instead, on the generated resolver to stay AOT safe:
    // https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/polymorphism#configure-polymorphism-with-the-contract-model
    private static void AddSections(JsonTypeInfo info)
    {
        var derived = info switch
        {
            JsonTypeInfo<CameraConfiguration> => CameraTypes,
            JsonTypeInfo<VideoMixerConfiguration> => VideoMixerTypes,
            JsonTypeInfo<InterfaceConfiguration> => InterfaceTypes,
            _ => null,
        };

        if (derived is null)
        {
            return;
        }

        info.PolymorphismOptions = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "type",
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };

        foreach (var type in derived)
        {
            info.PolymorphismOptions.DerivedTypes.Add(type);
        }
    }
}
