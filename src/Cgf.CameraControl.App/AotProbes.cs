using System.Globalization;
using Cgf.CameraControl.App.Localization;

namespace Cgf.CameraControl.App;

// Two libraries in this application fail under trimming without failing the build. AtemSharp finds
// its command types through Assembly.GetTypes(), which trimming can empty, leaving a build that
// connects to a switcher and decodes nothing. Localization would silently serve English if its
// strings stopped being reachable. The release workflow runs this against every published binary so
// that neither failure reaches a tag.
public static class AotProbes
{
    public static int Run()
    {
        var atem = Atem.AotProbe.Touch().GetAwaiter().GetResult();
        Console.WriteLine(atem.Report);
        Console.WriteLine(Input.Sdl.AotProbe.Touch());

        var localization = ProbeLocalization();
        Console.WriteLine(localization.Report);

        return atem.RegistryAlive && localization.Alive ? 0 : 1;
    }

    private static (bool Alive, string Report) ProbeLocalization()
    {
        var missing = new List<string>();
        var title = Localizer.Current["app.title"];
        var english = Latest(title);

        foreach (var language in Localizer.Languages)
        {
            Localizer.Current.Use(language);
            var translated = Latest(title);

            // A language whose file did not survive falls back, so English is the tell for
            // everything except English itself.
            if (translated == "app.title" || (language.Culture != Localizer.SourceLanguage && translated == english))
            {
                missing.Add(language.Culture);
            }
        }

        Localizer.Current.Use(Localizer.Match(CultureInfo.InvariantCulture));

        return missing.Count == 0
            ? (true, $"localization {Localizer.Languages.Count} languages -> OK: all resolve")
            : (false, $"localization -> FAIL: not embedded or not reachable: {string.Join(", ", missing)}");
    }

    private static string Latest(IObservable<string> source)
    {
        var latest = string.Empty;
        using (source.Subscribe(value => latest = value))
        {
            return latest;
        }
    }
}
