using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.App.Tests;

/// The sample is the file a new operator copies, so it has to load. Nothing else in the repository
/// is written to the interface schema by hand, which makes this the only place a schema change gets
/// caught before someone opens the file.
public class SampleConfigurationTests
{
    [Fact]
    public void TheShippedSampleLoads()
    {
        var config = ConfigLoader.LoadFile(Path(), out var issues);

        Assert.Empty(issues);
        Assert.NotEmpty(config.Interfaces);
    }

    [Fact]
    public void ItsKeyboardInterfaceBindsEveryKeyItDraws()
    {
        var entry = Assert.Single(ConfigLoader.LoadFile(Path(), out _).Interfaces);
        var keyboard = entry.Deserialize(InterfaceConfigurationContext.Default.KeyboardConfiguration);

        InterfaceValidation.Bindings(entry, keyboard);

        Assert.NotNull(keyboard.Keys.Pan);
        Assert.NotEmpty(keyboard.Keys.Input!);
        Assert.NotEmpty(keyboard.Functions);
    }

    /// The tests run out of the build output, and the sample is not copied there.
    private static string Path()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(System.IO.Path.Combine(directory.FullName, "samples")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return System.IO.Path.Combine(directory.FullName, "samples", "offline.json");
    }
}
