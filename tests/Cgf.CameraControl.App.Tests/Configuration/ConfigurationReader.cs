using Cgf.CameraControl.App.Configuration;
using Cgf.CameraControl.Core.Configuration;

namespace Cgf.CameraControl.App.Tests.Configuration;

internal static class ConfigurationReader
{
    /// The single entry of the section, read as the record its type names.
    public static T Read<T>(string section, string entry)
        where T : EntryConfiguration => Assert.IsType<T>(Entry(section, entry), exactMatch: false);

    /// Why the single entry of the section could not be read.
    public static string Rejected(string section, string entry) =>
        Assert.IsAssignableFrom<IInvalidConfiguration>(Entry(section, entry)).Problem;

    private static EntryConfiguration Entry(string section, string entry)
    {
        var config = ConfigLoader.Load($$"""{ "{{section}}": [ {{entry}} ] }""", out _);
        return section switch
        {
            "cams" => Assert.Single(config.Cams),
            "videoMixers" => Assert.Single(config.VideoMixers),
            _ => Assert.Single(config.Interfaces),
        };
    }
}
