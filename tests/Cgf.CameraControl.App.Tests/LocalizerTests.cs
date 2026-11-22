using Cgf.CameraControl.App.Localization;

namespace Cgf.CameraControl.App.Tests;

/// The window binds to observables, but a native menu header and the application's own name are
/// plain strings that have to be read at the moment they are set.
public class LocalizerTests
{
    private readonly Localizer _strings = new();

    [Fact]
    public void ReadsTheStringOfTheLanguageInUse()
    {
        Use("de");

        Assert.Equal("Kamerasteuerung", _strings.Text("app.title"));
    }

    [Fact]
    public void FollowsALanguageChange()
    {
        Use("de");
        var german = _strings.Text("app.title");

        Use("fr");

        Assert.NotEqual(german, _strings.Text("app.title"));
    }

    // A gap in a translation shows as the key rather than as nothing at all.
    [Fact]
    public void AKeyNobodyTranslatedReadsAsItself()
    {
        Assert.Equal("nothing.here", _strings.Text("nothing.here"));
    }

    private void Use(string culture) =>
        _strings.Use(Localizer.Languages.First(language => language.Culture == culture));
}
