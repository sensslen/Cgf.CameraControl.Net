using System.Text.Json;
using Cgf.CameraControl.App.Configuration;
using Cgf.CameraControl.App.Hosting;
using Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;
using Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;
using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;
using Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.VideoMixer.Passthrough;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.App.Tests.Configuration;

/// Every type string the application accepts, read and written through the source-generated context
/// with reflection-based serialization switched off for this assembly.
public class ConfigurationSchemaTests
{
    /// The interfaces name a mixer nobody configured, so building one stops before it claims a pad.
    private const string Pad =
        """
        "instance": 1, "videoMixer": 99, "cameraMap": {},
        "connectionChange": { "type": "direct", "default": {} }
        """;

    public static TheoryData<string, string, string> Entries { get; } = new()
    {
        { "cams", """ "ip": "192.0.2.1" """, ViscaOverIpCameraConfiguration.TypeName },
        { "cams", """ "ip": "192.0.2.2" """, WebsocketPtzLancCameraConfiguration.TypeName },
        { "cams", """ "connectionUrl": "http://192.0.2.3:5000", "connectionPort": "COM6" """, SignalrPtzLancCameraConfiguration.TypeName },
        { "videoMixers", """ "ip": "192.0.2.4", "mixEffectBlock": 0 """, AtemConfiguration.TypeName },
        { "videoMixers", string.Empty, PassthroughConfiguration.TypeName },
        { "interfaces", Pad, GamepadConfiguration.TypeName },
        { "interfaces", Pad, LogitechF310Configuration.TypeName },
        { "interfaces", Pad, LogitechF710Configuration.TypeName },
        { "interfaces", Pad, LogitechRumblepad2Configuration.TypeName },
        { "interfaces", Pad + """, "keys": {}""", KeyboardConfiguration.TypeName },
    };

    /// Each type reads as the record that carries its name, wherever among the entry's properties it
    /// stands, and is written back under that name.
    [Theory]
    [MemberData(nameof(Entries))]
    public void EveryTypeReadsAsItsRecord(string section, string properties, string type)
    {
        var instance = section == "interfaces" ? string.Empty : "\"instance\": 1";
        var body = string.Join(", ", new[] { instance, properties.Trim() }.Where(part => part.Length > 0));

        foreach (var entry in new[] { $$"""{ "type": "{{type}}", {{body}} }""", $$"""{ {{body}}, "type": "{{type}}" }""" })
        {
            var config = ConfigLoader.Load($$"""{ "{{section}}": [ {{entry}} ] }""", out var issues);

            Assert.Empty(issues);
            Assert.Equal(type, Assert.Single(AllEntries(config)).Type);
            Assert.Contains($"\"type\": \"{type}\"", ConfigWriter.Write(config), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryRegisteredTypeIsCovered()
    {
        var registered = ConfigurationSections.CameraTypes
            .Concat(ConfigurationSections.VideoMixerTypes)
            .Concat(ConfigurationSections.InterfaceTypes)
            .Select(derived => Assert.IsType<string>(derived.TypeDiscriminator));

        Assert.Equal(
            Entries.Select(row => row.Data.Item3).Order(StringComparer.Ordinal),
            registered.Order(StringComparer.Ordinal));
    }

    /// One desk holding every registered record, loaded, written and loaded again unchanged.
    [Fact]
    public void ADeskOfEveryRecordSurvivesARoundTrip()
    {
        Assert.False(JsonSerializer.IsReflectionEnabledByDefault);

        var config = ConfigLoader.Load(Desk(), out var issues);

        Assert.Empty(issues);
        var written = ConfigWriter.Write(config);
        var reread = ConfigLoader.Load(written, out var reissues);
        Assert.Empty(reissues);
        Assert.Equal(written, ConfigWriter.Write(reread));
        Assert.Equal(Entries.Select(row => row.Data.Item3), AllEntries(reread).Select(entry => entry.Type));
    }

    /// A legacy pad name is written back as the name it was read as, which is what keeps the HID
    /// backend refused on the two pads it cannot decode.
    [Fact]
    public void AnAliasKeepsItsNameAndItsRules()
    {
        var config = ConfigLoader.Load(
            $$"""{ "interfaces": [ { "type": "logitech/F710", {{Pad}} } ] }""", out _);

        var pad = Assert.IsType<LogitechF710Configuration>(Assert.Single(config.Interfaces));
        Assert.False(pad.SupportsHid);
        Assert.Contains("\"type\": \"logitech/F710\"", ConfigWriter.Write(config), StringComparison.Ordinal);
    }

    internal static string Desk()
    {
        var sections = Entries
            .Select((row, index) => (Section: row.Data.Item1, Entry: Entry(row.Data.Item1, row.Data.Item2, row.Data.Item3, index + 1)))
            .GroupBy(row => row.Section)
            .Select(group => $$"""
                "{{group.Key}}": [ {{string.Join(", ", group.Select(row => row.Entry))}} ]
                """);
        return $$"""{ {{string.Join(", ", sections)}} }""";
    }

    private static string Entry(string section, string properties, string type, int instance)
    {
        var body = section == "interfaces"
            ? properties.Replace("\"instance\": 1", $"\"instance\": {instance}", StringComparison.Ordinal)
            : $"\"instance\": {instance}" + (properties.Trim().Length > 0 ? $", {properties.Trim()}" : string.Empty);
        return $$"""{ "type": "{{type}}", {{body}} }""";
    }

    private static IEnumerable<EntryConfiguration> AllEntries(RootConfig config) =>
        config.Cams.Concat<EntryConfiguration>(config.VideoMixers).Concat(config.Interfaces);
}

/// The host opens the pad backends, which run one at a time with the other tests that build one.
[Collection("Avalonia UI")]
public class ConfigurationSchemaHostTests
{
    /// A record no builder claims would load and then be reported as unbuildable.
    [Fact]
    public async Task EveryRegisteredTypeHasABuilder()
    {
        await using var host = new AppHost();
        var config = ConfigLoader.Load(ConfigurationSchemaTests.Desk(), out _);

        var issues = await host.Core.ReconfigureAsync(config, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(issues, issue => issue.Message.Contains("no builder claims", StringComparison.Ordinal));
        Assert.Equal(3, host.Core.CameraFactory.Instances.Count);
        Assert.Equal(2, host.Core.MixerFactory.Instances.Count);
    }
}
