using Cgf.CameraControl.App.Localization;

namespace Cgf.CameraControl.App.Tests;

/// The window binds to observables, but a native menu header and the application's own name are
/// plain strings that have to be read at the moment they are set.
public class LocalizerTests : IDisposable
{
    private readonly Language _before = Localizer.Current.Active;

    public void Dispose() => Localizer.Current.Use(_before);

    [Fact]
    public void ReadsTheStringOfTheLanguageInUse()
    {
        Use("de");

        Assert.Equal("Kamerasteuerung", Localizer.Current.Text("app.title"));
    }

    [Fact]
    public void FollowsALanguageChange()
    {
        Use("de");
        var german = Localizer.Current.Text("app.title");

        Use("fr");

        Assert.NotEqual(german, Localizer.Current.Text("app.title"));
    }

    // A gap in a translation shows as the key rather than as nothing at all.
    [Fact]
    public void AKeyNobodyTranslatedReadsAsItself()
    {
        Assert.Equal("nothing.here", Localizer.Current.Text("nothing.here"));
    }

    private static void Use(string culture) =>
        Localizer.Current.Use(Localizer.Languages.First(language => language.Culture == culture));
}
