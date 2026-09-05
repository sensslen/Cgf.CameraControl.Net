using Cgf.CameraControl.App.Licenses;

namespace Cgf.CameraControl.App.Tests;

/// The list is written by a source generator reading the committed nuget-license report. A build
/// that stopped generating it still compiles, because an empty list is a valid list, and the window
/// would then credit nobody.
public class ThirdPartyLicenseTests
{
    [Fact]
    public void ThePackagesThisShipsAreCompiledIn()
    {
        Assert.NotEmpty(ThirdPartyLicenses.All);
    }

    [Fact]
    public void EveryPackageIsNamedAndLicensed()
    {
        Assert.All(ThirdPartyLicenses.All, package =>
        {
            Assert.NotEmpty(package.Title);
            Assert.False(string.IsNullOrWhiteSpace(package.License));
        });
    }

    [Fact]
    public void TheLibrariesTheCamerasAreBuiltOnAreCredited()
    {
        var titles = ThirdPartyLicenses.All.Select(package => package.Title).ToList();

        Assert.Contains(titles, title => title.StartsWith("Avalonia ", StringComparison.Ordinal));
        Assert.Contains(titles, title => title.StartsWith("AtemSharp ", StringComparison.Ordinal));
        Assert.Contains(titles, title => title.StartsWith("Microsoft.AspNetCore.SignalR.Client ", StringComparison.Ordinal));
    }
}
