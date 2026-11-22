using System.Text.Json.Nodes;
using Cgf.CameraControl.App.Hosting;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.App.Tests;

/// The samples are the files a new operator copies, so they have to load. Nothing else in the
/// repository is written to the interface schema by hand, which makes this the only place a schema
/// change gets caught before someone opens the file.
public class SampleConfigurationTests
{
    public static TheoryData<string> Samples { get; } = ["offline.json", "two-operators.json"];

    [Theory]
    [MemberData(nameof(Samples))]
    public void TheShippedSampleLoads(string sample)
    {
        var config = ConfigLoader.LoadFile(Path(sample), AppHost.Schema, out var issues);

        Assert.Empty(issues);
        Assert.NotEmpty(config.Interfaces);
    }

    /// Saving a sample that nobody edited says the same thing it said before, apart from the defaults
    /// it spelled out, which the writer leaves out.
    [Theory]
    [MemberData(nameof(Samples))]
    public void TheShippedSampleIsSavedAsItWasWritten(string sample)
    {
        var original = File.ReadAllText(Path(sample));

        var written = ConfigWriter.Write(ConfigLoader.Load(original, AppHost.Schema, out _), AppHost.Schema);

        Assert.True(JsonNode.DeepEquals(WithoutDefaults(JsonNode.Parse(original)!), JsonNode.Parse(written)), written);
        Assert.Equal(written, ConfigWriter.Write(ConfigLoader.Load(written, AppHost.Schema, out _), AppHost.Schema));
    }

    [Fact]
    public void ItsKeyboardInterfaceBindsEveryKeyItDraws()
    {
        var keyboard = Assert.IsType<KeyboardConfiguration>(
            Assert.Single(ConfigLoader.LoadFile(Path("offline.json"), AppHost.Schema, out _).Interfaces));

        InterfaceValidation.Bindings(keyboard);

        Assert.NotNull(keyboard.Keys.Pan);
        Assert.NotEmpty(keyboard.Keys.Input!);
        Assert.NotEmpty(keyboard.Functions);
    }

    /// The two defaults the samples spell out.
    private static JsonNode WithoutDefaults(JsonNode root)
    {
        foreach (var entry in root["interfaces"]!.AsArray())
        {
            var interfaceEntry = entry!.AsObject();
            if (interfaceEntry["deadzone"]?.GetValue<double>() == 0.05)
            {
                interfaceEntry.Remove("deadzone");
            }

            if (interfaceEntry["rumble"]?.GetValue<bool>() == true)
            {
                interfaceEntry.Remove("rumble");
            }
        }

        return root;
    }

    /// The tests run out of the build output, and the samples are not copied there.
    private static string Path(string sample)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(System.IO.Path.Combine(directory.FullName, "samples")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return System.IO.Path.Combine(directory.FullName, "samples", sample);
    }
}
