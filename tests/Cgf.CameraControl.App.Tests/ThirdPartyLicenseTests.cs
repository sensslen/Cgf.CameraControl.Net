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

    // A licence named with no text behind it is a notices screen that cannot show what it promises.
    [Fact]
    public void EveryPackageCarriesTheTextOfItsLicence()
    {
        Assert.All(ThirdPartyLicenses.All, package =>
        {
            Assert.NotNull(package.Text);

            // Every licence this ships under disclaims warranty, so a text without the word is one
            // that did not arrive whole.
            Assert.Contains("warrant", package.Text, StringComparison.OrdinalIgnoreCase);
        });
    }

    // Forty-eight MIT packages are one MIT text, because the compiler keeps one copy of a literal
    // however many times the generator writes it.
    [Fact]
    public void PackagesUnderOneLicenceShareOneText()
    {
        var texts = ThirdPartyLicenses.All
            .Where(package => package.License == "MIT")
            .Select(package => package.Text)
            .ToList();

        Assert.All(texts, text => Assert.Same(texts[0], text));
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
