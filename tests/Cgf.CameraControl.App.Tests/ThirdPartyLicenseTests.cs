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
    public void EveryLicenceNamedHasItsTextCompiledIn()
    {
        var texts = ThirdPartyLicenses.Texts.Select(text => text.Name).ToList();

        Assert.All(ThirdPartyLicenses.All, package => Assert.Contains(package.License, texts));
    }

    [Fact]
    public void EveryTextIsTheLicenceItIsNamedAfter()
    {
        Assert.All(ThirdPartyLicenses.Texts, text =>
        {
            Assert.NotEmpty(text.Name);

            // Every licence this ships under disclaims warranty, so a text without the word is a
            // file that did not arrive whole.
            Assert.Contains("warrant", text.Text, StringComparison.OrdinalIgnoreCase);
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
