using Cgf.CameraControl.App.Configuration;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.Core.Configuration;

namespace Cgf.CameraControl.App;

// Four things in this application fail under trimming without failing the build. AtemSharp finds
// its command types through Assembly.GetTypes(), which trimming can empty, leaving a build that
// connects to a switcher and decodes nothing. The SignalR camera serializes its state through a
// resolver chain that has nothing behind the generated resolver, so a camera can connect and then
// fail every update. Localization would silently serve English if its strings stopped being
// reachable. The configuration's section polymorphism is added to the generated contract at run
// time, so a record the trimmer cut off would only show as entries that no longer load. The release
// workflow runs this against every published binary so that none of the four reaches a tag.
public static class AotProbes
{
    public static int Run()
    {
        var atem = Atem.AotProbe.Touch().GetAwaiter().GetResult();
        Console.WriteLine(atem.Report);
        Console.WriteLine(Input.Sdl.AotProbe.Touch());

        var signalr = Cameras.SignalrPtzLanc.AotProbe.Touch();
        Console.WriteLine(signalr.Report);

        var localization = ProbeLocalization();
        Console.WriteLine(localization.Report);

        var configuration = ProbeConfiguration();
        Console.WriteLine(configuration.Report);

        return atem.RegistryAlive && signalr.Alive && localization.Alive && configuration.Alive ? 0 : 1;
    }

    /// One entry of every registered type, read, written and read again.
    private static (bool Alive, string Report) ProbeConfiguration()
    {
        const string Desk =
            """
            {
              "cams": [
                { "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1", "tallyMode": "avonic" },
                { "type": "Websocket.PtzLanc", "instance": 2, "ip": "10.0.0.2" },
                { "type": "Signalr.PtzLanc", "instance": 3, "connectionUrl": "http://10.0.0.3:5000", "connectionPort": "COM6" }
              ],
              "videoMixers": [
                { "type": "blackmagicdesign/atem", "instance": 1, "ip": "10.0.0.4", "mixEffectBlock": 0 },
                { "type": "passthrough/default", "instance": 2, "tallyMode": "program" }
              ],
              "interfaces": [
                { "type": "gamepad", "instance": 1, "videoMixer": 1, "cameraMap": { "1": 1 },
                  "connectionChange": { "type": "direct", "default": { "up": 1 } },
                  "functions": { "wide": { "type": "key", "index": 1 } }, "pad": { "default": { "up": "wide" } } },
                { "type": "logitech/F310", "instance": 2, "videoMixer": 1, "cameraMap": {},
                  "connectionChange": { "type": "directional", "directions": { "1": { "right": 2 } } } },
                { "type": "logitech/F710", "instance": 3, "videoMixer": 1, "cameraMap": {},
                  "connectionChange": { "type": "direct", "default": {} } },
                { "type": "logitech/Rumblepad2", "instance": 4, "videoMixer": 1, "cameraMap": {},
                  "connectionChange": { "type": "direct", "default": {} } },
                { "type": "keyboard", "instance": 5, "videoMixer": 1, "cameraMap": {}, "keys": { "cut": "Enter" },
                  "connectionChange": { "type": "direct", "default": {} } }
              ]
            }
            """;

        try
        {
            var read = ConfigLoader.Load(Desk, out var issues);
            var again = ConfigLoader.Load(ConfigWriter.Write(read), out var reissues);
            var types = again.Cams.Concat<EntryConfiguration>(again.VideoMixers).Concat(again.Interfaces)
                .Select(entry => entry.Type)
                .ToList();
            var problems = issues.Concat(reissues).ToList();
            return problems.Count == 0 && types.Count == 10
                ? (true, $"configuration {types.Count} types -> OK: all read and written")
                : (false, $"configuration -> FAIL: {string.Join("; ", problems)}");
        }
        catch (Exception ex)
        {
            return (false, $"configuration -> FAIL: {ex.Message}");
        }
    }

    private static (bool Alive, string Report) ProbeLocalization()
    {
        var missing = new List<string>();
        var strings = new Localizer();
        var title = strings["app.title"];
        var english = Latest(title);

        foreach (var language in Localizer.Languages)
        {
            strings.Use(language);
            var translated = Latest(title);

            // A language whose file did not survive falls back, so English is the tell for
            // everything except English itself.
            if (translated == "app.title" || (language.Culture != Localizer.SourceLanguage && translated == english))
            {
                missing.Add(language.Culture);
            }
        }

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
