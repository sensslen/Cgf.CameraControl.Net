using Cgf.CameraControl.App.Hosting;
using Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;
using Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;
using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;
using Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.VideoMixer.Passthrough;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.App.Tests;

/// Every type string the application accepts, read through the schema the application loads with.
public class ConfigurationSchemaTests
{
    private const string Pad =
        """
        "instance": 1, "videoMixer": 1, "cameraMap": {},
        "connectionChange": { "type": "direct", "default": {} }
        """;

    public static TheoryData<string, string, Type> Entries { get; } = new()
    {
        { "cams", """ "ip": "10.0.0.1" """, typeof(ViscaOverIpCameraConfiguration) },
        { "cams", """ "ip": "10.0.0.1" """, typeof(WebsocketPtzLancCameraConfiguration) },
        { "cams", """ "connectionUrl": "http://10.0.0.1:5000", "connectionPort": "COM6" """, typeof(SignalrPtzLancCameraConfiguration) },
        { "videoMixers", """ "ip": "10.0.0.1", "mixEffectBlock": 0 """, typeof(AtemConfiguration) },
        { "videoMixers", string.Empty, typeof(PassthroughConfiguration) },
        { "interfaces", Pad, typeof(GamepadConfiguration) },
        { "interfaces", Pad, typeof(LogitechF310Configuration) },
        { "interfaces", Pad, typeof(LogitechF710Configuration) },
        { "interfaces", Pad, typeof(LogitechRumblepad2Configuration) },
        { "interfaces", Pad + """, "keys": {}""", typeof(KeyboardConfiguration) },
    };

    /// Each type reads as its own record, whatever case the file spells it in and wherever among the
    /// entry's properties it stands, and is written back as registered.
    [Theory]
    [MemberData(nameof(Entries))]
    public void EveryTypeReadsAsItsRecord(string section, string properties, Type record)
    {
        var name = TypeName(record);
        var instance = section == "interfaces" ? string.Empty : "\"instance\": 1";
        var body = string.Join(", ", new[] { instance, properties.Trim() }.Where(part => part.Length > 0));

        foreach (var spelling in new[] { name, name.ToUpperInvariant(), name.ToLowerInvariant() })
        {
            foreach (var entry in new[] { $$"""{ "type": "{{spelling}}", {{body}} }""", $$"""{ {{body}}, "type": "{{spelling}}" }""" })
            {
                var config = ConfigLoader.Load($$"""{ "{{section}}": [ {{entry}} ] }""", AppHost.Schema, out var issues);

                Assert.Empty(issues);
                var read = Assert.Single(config.Cams.Concat<EntryConfiguration>(config.VideoMixers).Concat(config.Interfaces));
                Assert.IsType(record, read);
                Assert.Equal(name, read.Type);
                Assert.Contains($"\"type\": \"{name}\"", ConfigWriter.Write(config, AppHost.Schema), StringComparison.Ordinal);
            }
        }
    }

    /// The type string a record carries is the one it is registered under, so what the editor and
    /// the errors call an entry is what the file calls it.
    [Fact]
    public void EveryRecordIsRegisteredUnderItsOwnTypeName()
    {
        var registered = AppHost.Schema.TypesOf<CameraConfiguration>()
            .Concat(AppHost.Schema.TypesOf<VideoMixerConfiguration>())
            .Concat(AppHost.Schema.TypesOf<InterfaceConfiguration>());

        Assert.Equal(
            Entries.Select(row => TypeName(row.Data.Item3)).Order(StringComparer.Ordinal),
            registered.Order(StringComparer.Ordinal));
    }

    /// A legacy pad name is written back as the name it was read as, which is what keeps the HID
    /// backend refused on the two pads it cannot decode.
    [Fact]
    public void AnAliasKeepsItsNameAndItsRules()
    {
        var config = ConfigLoader.Load(
            $$"""{ "interfaces": [ { "type": "logitech/f710", {{Pad}} } ] }""", AppHost.Schema, out _);

        var pad = Assert.IsType<LogitechF710Configuration>(Assert.Single(config.Interfaces));
        Assert.False(pad.SupportsHid);
        Assert.Contains("\"type\": \"logitech/F710\"", ConfigWriter.Write(config, AppHost.Schema), StringComparison.Ordinal);
    }

    private static string TypeName(Type record) => ((EntryConfiguration)Activator.CreateInstance(record)!).Type;
}

/// The host opens the pad backends, which run one at a time with the other tests that build one.
[Collection("Avalonia UI")]
public class ConfigurationSchemaHostTests
{
    /// A builder whose type has no record would never see an entry, and a record no builder claims
    /// would load and then be reported as unbuildable.
    [Fact]
    public async Task EveryRegisteredTypeHasABuilder()
    {
        await using var host = new AppHost();

        Assert.Equal(
            AppHost.Schema.TypesOf<CameraConfiguration>().Order(StringComparer.Ordinal),
            host.Core.CameraFactory.ClaimedTypes.Order(StringComparer.Ordinal));
        Assert.Equal(
            AppHost.Schema.TypesOf<VideoMixerConfiguration>().Order(StringComparer.Ordinal),
            host.Core.MixerFactory.ClaimedTypes.Order(StringComparer.Ordinal));
        Assert.Equal(
            AppHost.Schema.TypesOf<InterfaceConfiguration>().Order(StringComparer.Ordinal),
            host.Core.HmiFactory.ClaimedTypes.Order(StringComparer.Ordinal));
    }
}
